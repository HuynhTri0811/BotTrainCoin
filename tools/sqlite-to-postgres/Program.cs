using System.Diagnostics;
using System.Globalization;
using CryptoBot.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

// Chép toàn bộ dữ liệu từ file SQLite cũ (bản trước khi chuyển sang PostgreSQL) vào PostgreSQL.
//   dotnet run --project tools/sqlite-to-postgres -- <file .db> "<chuỗi kết nối PostgreSQL>"
// Áp migration trước, chỉ chép vào database còn trống, giữ nguyên Id rồi đặt lại bộ đếm Id, cuối cùng đếm lại từng bảng.
// File SQLite chỉ được mở để đọc.
if (args.Length != 2)
{
    Console.Error.WriteLine("Dùng: dotnet run --project tools/sqlite-to-postgres -- <file .db> \"<chuỗi kết nối PostgreSQL>\"");
    return 2;
}

var sqlitePath = Path.GetFullPath(args[0]);
if (!File.Exists(sqlitePath))
{
    Console.Error.WriteLine($"Không thấy file SQLite: {sqlitePath}");
    return 2;
}

await using var db = new BotDbContext(new DbContextOptionsBuilder<BotDbContext>().UseNpgsql(args[1]).Options);
await db.Database.MigrateAsync();

// Bảng được tham chiếu (khóa ngoại) chép trước bảng tham chiếu tới nó.
var tables = ParentsFirst(db.Model.GetEntityTypes().OrderBy(entity => entity.GetTableName()).ToList())
    .Select(entity => (Entity: entity, Table: entity.GetTableName()!))
    .ToList();

await using var target = new NpgsqlConnection(args[1]);
await target.OpenAsync();

// An toàn: không trộn với dữ liệu đã có.
foreach (var (_, table) in tables)
{
    if (await CountAsync(target, table) > 0)
    {
        Console.Error.WriteLine($"Bảng \"{table}\" trên PostgreSQL đã có dữ liệu; chỉ chép vào database trống.");
        return 1;
    }
}

await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
{
    DataSource = sqlitePath,
    Mode = SqliteOpenMode.ReadOnly,
}.ConnectionString);
await source.OpenAsync();

var failed = false;
foreach (var (entity, table) in tables)
{
    var watch = Stopwatch.StartNew();
    var columns = entity.GetProperties()
        .Select(property => (Property: property, Column: property.GetColumnName()))
        .ToList();
    var copied = await CopyTableAsync(source, target, table, columns);
    await ResetIdentityAsync(target, entity, table);

    var sourceCount = await CountAsync(source, table);
    var targetCount = await CountAsync(target, table);
    var ok = sourceCount == targetCount && copied == sourceCount;
    failed |= !ok;
    Console.WriteLine($"{(ok ? "OK  " : "LỆCH")} {table,-20} SQLite {sourceCount,9:N0} → PostgreSQL {targetCount,9:N0} ({watch.Elapsed.TotalSeconds:0.0}s)");
}

return failed ? 1 : 0;

static async Task<long> CopyTableAsync(SqliteConnection source, NpgsqlConnection target, string table,
    List<(IProperty Property, string Column)> columns)
{
    var columnList = string.Join(", ", columns.Select(column => $"\"{column.Column}\""));
    await using var read = source.CreateCommand();
    read.CommandText = $"SELECT {columnList} FROM \"{table}\"";
    await using var reader = await read.ExecuteReaderAsync();

    await using var writer = await target.BeginBinaryImportAsync($"COPY \"{table}\" ({columnList}) FROM STDIN (FORMAT BINARY)");
    long rows = 0;
    while (await reader.ReadAsync())
    {
        await writer.StartRowAsync();
        for (var index = 0; index < columns.Count; index++)
        {
            var value = reader.IsDBNull(index) ? null : Convert(reader.GetValue(index), columns[index].Property);
            if (value is null)
                await writer.WriteNullAsync();
            else
                await writer.WriteAsync(value);
        }

        rows++;
    }

    await writer.CompleteAsync();
    return rows;
}

// Giá trị SQLite cũ → kiểu của cột PostgreSQL mới. Bản SQLite lưu decimal dạng chuỗi invariant, thời gian dạng
// Unix milliseconds UTC, bool dạng 0/1, Guid dạng chuỗi; enum đã là chuỗi ở cả hai bên.
static object Convert(object raw, IProperty property)
{
    // Enum lưu dạng chuỗi: kiểu lưu trong DB nằm ở type mapping, không phải ClrType của property.
    var type = property.GetTypeMapping().Converter?.ProviderClrType ?? property.ClrType;
    type = Nullable.GetUnderlyingType(type) ?? type;

    if (type == typeof(decimal))
        return decimal.Parse(System.Convert.ToString(raw, CultureInfo.InvariantCulture)!, NumberStyles.Float,
            CultureInfo.InvariantCulture);
    if (type == typeof(DateTime))
        return DateTime.UnixEpoch.AddMilliseconds(System.Convert.ToInt64(raw, CultureInfo.InvariantCulture));
    if (type == typeof(bool))
        return System.Convert.ToInt64(raw, CultureInfo.InvariantCulture) != 0;
    if (type == typeof(Guid))
        return raw is byte[] bytes ? new Guid(bytes) : Guid.Parse((string)raw);
    if (type == typeof(int))
        return System.Convert.ToInt32(raw, CultureInfo.InvariantCulture);
    if (type == typeof(long))
        return System.Convert.ToInt64(raw, CultureInfo.InvariantCulture);
    if (type == typeof(string))
        return System.Convert.ToString(raw, CultureInfo.InvariantCulture)!;
    if (type == typeof(byte[]))
        return (byte[])raw;
    throw new NotSupportedException($"Chưa chép được kiểu {type.Name} (cột {property.Name}).");
}

static List<IEntityType> ParentsFirst(List<IEntityType> entities)
{
    var ordered = new List<IEntityType>();
    void Visit(IEntityType entity)
    {
        if (ordered.Contains(entity))
            return;
        foreach (var foreignKey in entity.GetForeignKeys())
            if (foreignKey.PrincipalEntityType != entity)
                Visit(foreignKey.PrincipalEntityType);
        ordered.Add(entity);
    }

    entities.ForEach(Visit);
    return ordered;
}

// Id được chép nguyên: đặt bộ đếm Id về Id lớn nhất, để dòng mới không trùng Id cũ.
static async Task ResetIdentityAsync(NpgsqlConnection target, IEntityType entity, string table)
{
    foreach (var key in entity.FindPrimaryKey()!.Properties.Where(property => property.ValueGenerated == ValueGenerated.OnAdd))
    {
        var column = key.GetColumnName();
        await using var command = target.CreateCommand();
        command.CommandText =
            $"SELECT setval(pg_get_serial_sequence('\"{table}\"', '{column}'), " +
            $"COALESCE((SELECT MAX(\"{column}\") FROM \"{table}\"), 0) + 1, false)";
        await command.ExecuteScalarAsync();
    }
}

static async Task<long> CountAsync(System.Data.Common.DbConnection connection, string table)
{
    await using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
    return System.Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}
