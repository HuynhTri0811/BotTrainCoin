namespace CryptoBot.Domain.Strategies;

/// <summary>Biến dữ liệu đầu vào thành quyết định. Không gọi API sàn, không đọc đồng hồ hệ thống.</summary>
public interface IStrategy
{
    /// <summary>Số nến đã đóng tối thiểu cần có trong cửa sổ trước khi được ra quyết định.</summary>
    int RequiredCandles { get; }

    StrategyDecision Evaluate(StrategyContext context);
}
