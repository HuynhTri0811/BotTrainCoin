namespace CryptoBot.Application.News;

/// <summary>Kết quả một lượt đồng bộ: số bài mới và lỗi theo nguồn.</summary>
public sealed record NewsSyncResult(int Added, IReadOnlyList<string> Errors);
