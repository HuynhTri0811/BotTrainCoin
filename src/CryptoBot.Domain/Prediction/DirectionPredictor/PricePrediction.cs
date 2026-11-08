namespace CryptoBot.Domain.Prediction;

/// <summary>Kết quả dự đoán. Score &gt;= 0 là Up, &lt; 0 là Down; |Score| càng lớn càng "chắc".</summary>
public sealed record PricePrediction(PriceDirection Direction, decimal Score, string Reason);
