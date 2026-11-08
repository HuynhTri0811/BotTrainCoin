namespace CryptoBot.Api.Endpoints;

/// <summary>Mua/bán chủ động của nến. Side: Balanced | BuyHeavy | SellHeavy so với AlertExcessPercent.</summary>
public sealed record VolumePressureDto(decimal BuyVolume, decimal SellVolume, decimal BuyPercent,
    decimal? ExcessPercent, string Side, decimal AlertExcessPercent);
