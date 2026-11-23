using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;
using CryptoBot.Domain.Futures;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace CryptoBot.Infrastructure.Persistence;

public sealed class AutoOrderStore(BotDbContext db) : IAutoOrderStore
{
    private const string Env = "Paper";

    public async Task<IReadOnlyList<AutoOrderRule>> GetActiveAsync(CancellationToken ct) =>
        await db.AutoOrderRules
            .Where(rule => rule.Environment == Env && rule.Status == AutoRuleStatus.Active)
            .OrderBy(rule => rule.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetWatchingCloseAsync(CancellationToken ct) =>
        await db.AutoOrderRules
            .Where(rule => rule.Environment == Env && rule.CloseOnReverse && rule.Status == AutoRuleStatus.Triggered &&
                        rule.CloseWatchEndedAtUtc == null)
            .OrderBy(rule => rule.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<AutoOrderRule>> GetRecentAsync(int limit, CancellationToken ct) =>
        await db.AutoOrderRules.AsNoTracking()
            .Where(rule => rule.Environment == Env)
            .OrderByDescending(rule => rule.Id).Take(limit).ToListAsync(ct);

    /// <summary>
    /// Đọc quy tắc với trạng thái mới nhất trong database. Quy tắc đã được DbContext này đọc trước đó (ví dụ trong
    /// danh sách Active của worker) được tải lại, vì truy vấn lại không làm mới entity đã tracking.
    /// </summary>
    public async Task<AutoOrderRule?> GetAsync(long id, CancellationToken ct)
    {
        AutoOrderRule? tracked = db.AutoOrderRules.Local.FirstOrDefault(rule => rule.Id == id);
        if (tracked is null)
            return await db.AutoOrderRules.FirstOrDefaultAsync(rule => rule.Environment == Env && rule.Id == id, ct);

        EntityEntry<AutoOrderRule> entry = db.Entry(tracked);
        if (entry.State == EntityState.Added)
            return tracked;
        await entry.ReloadAsync(ct);
        return entry.State == EntityState.Detached ? null : tracked;
    }

    public void Add(AutoOrderRule rule) => db.AutoOrderRules.Add(rule);

    /// <summary>
    /// Quy tắc mang concurrency token (xmin): lưu từ bản đọc cũ (ví dụ ghi Triggered lên quy tắc đã Cancelled) bị từ
    /// chối, các quy tắc bị đổi được đọc lại và ném <see cref="ConcurrentUpdateException"/>.
    /// </summary>
    public async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            foreach (EntityEntry entry in exception.Entries)
                await entry.ReloadAsync(ct);
            throw new ConcurrentUpdateException("Quy tắc đã bị thay đổi bởi thao tác khác; không lưu thay đổi.",
                exception);
        }
    }
}
