namespace CryptoBot.Api.Endpoints;

public sealed record AutoSummaryDto(IReadOnlyList<AutoRuleDto> Rules, AutoDefaultsDto Defaults, DailyRiskDto Risk);
