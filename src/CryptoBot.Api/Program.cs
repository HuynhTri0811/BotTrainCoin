using System.Globalization;
using CryptoBot.Api.Endpoints;
using CryptoBot.Api.Workers;
using CryptoBot.Application.Futures;
using CryptoBot.Application.Indicators;
using CryptoBot.Application.Signals;
using CryptoBot.Infrastructure;
using Serilog;

// Số trong thông báo lỗi dùng dấu chấm thập phân, khớp với giao diện, không phụ thuộc locale máy chủ.
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(args);
// Sự kiện thị trường để đối chiếu với tháng tăng/giảm mạnh; sửa file rồi restart API.
builder.Configuration.AddJsonFile("marketevents.json", optional: true);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<MarketDataWorker>();
builder.Services.AddHostedService<PaperFuturesWorker>();
builder.Services.AddHostedService<LivePriceWorker>();
builder.Services.AddHostedService<DailyHistoryWorker>();
builder.Services.AddHostedService<DailyReportWorker>();
// Cho phép client gửi số tiền dạng chuỗi ("50.5") để không mất chính xác qua JSON number.
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString);

var app = builder.Build();

await DependencyInjection.InitializeDatabaseAsync(app.Services, CancellationToken.None);
// Tạo registry ngay để lỗi cấu hình chỉ báo hiện trong log lúc khởi động.
app.Services.GetRequiredService<IndicatorRegistry>();
await using (var scope = app.Services.CreateAsyncScope())
{
    var definitions = app.Configuration.GetSection("Strategies").Get<List<StrategyDefinition>>() ?? [];
    await scope.ServiceProvider.GetRequiredService<StrategyConfigurator>().ApplyAsync(definitions, CancellationToken.None);
    await scope.ServiceProvider.GetRequiredService<PaperFuturesService>().EnsureFundedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseSerilogRequestLogging();

// Production: phục vụ bản build React (copy vào wwwroot) cùng một ứng dụng.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapBotEndpoints();
app.MapPaperFuturesEndpoints();
app.MapAutoOrderEndpoints();
app.MapIndicatorEndpoints();
app.MapLivePriceEndpoints();
app.MapPredictionEndpoints();
app.MapHistoryEndpoints();
app.MapReportEndpoints();
// API không tồn tại phải là 404 JSON, không bao giờ rơi vào trang SPA.
app.MapFallback("/api/{**path}", () => Results.NotFound(new { error = "Endpoint không tồn tại" }));
app.MapFallbackToFile("index.html");

app.Run();
