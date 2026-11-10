namespace CryptoBot.Api.Endpoints;

/// <summary>Một sàn chứng khoán mở cửa trong nến; Time là Unix seconds UTC của lúc mở cửa.</summary>
public sealed record MarketOpeningDto(string Code, string Name, string Exchange, long Time);
