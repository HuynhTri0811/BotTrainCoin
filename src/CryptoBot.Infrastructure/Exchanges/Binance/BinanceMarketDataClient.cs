using System.Globalization;
using System.Net;
using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Domain.MarketData;
using Microsoft.Extensions.Logging;

namespace CryptoBot.Infrastructure.Exchanges.Binance;

public sealed class BinanceOptions
{
    /// <summary>REST base URL cho dữ liệu thị trường công khai (không cần API key).</summary>
    public string MarketDataBaseUrl { get; set; } = "https://api.binance.com";

    /// <summary>REST base URL USDⓈ-M Futures cho mark price công khai.</summary>
    public string FuturesMarketDataBaseUrl { get; set; } = "https://fapi.binance.com";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Dữ liệu thị trường Spot qua REST công khai. Chỉ đọc; không ký request, không có key.
/// Tham khảo: https://github.com/binance/binance-spot-api-docs/blob/master/rest-api.md
/// </summary>
public sealed class BinanceMarketDataClient(HttpClient http, ILogger<BinanceMarketDataClient> logger) : IMarketDataClient
{
    public string Exchange => "Binance";

    public async Task<DateTime> GetServerTimeAsync(CancellationToken ct)
    {
        using var doc = await GetJsonAsync("/api/v3/time", ct);
        return FromUnixMs(doc.RootElement.GetProperty("serverTime").GetInt64());
    }

    public async Task<decimal> GetLastPriceAsync(string symbol, CancellationToken ct)
    {
        using var doc = await GetJsonAsync($"/api/v3/ticker/price?symbol={Uri.EscapeDataString(symbol)}", ct);
        return ParseDecimal(doc.RootElement.GetProperty("price"));
    }

    public async Task<IReadOnlyList<KlineDto>> GetKlinesAsync(string symbol, CandleInterval interval,
        DateTime startTimeUtc, int limit, CancellationToken ct)
    {
        var startMs = new DateTimeOffset(startTimeUtc).ToUnixTimeMilliseconds();
        var url = $"/api/v3/klines?symbol={Uri.EscapeDataString(symbol)}&interval={interval.Code}" +
                  $"&startTime={startMs}&limit={Math.Clamp(limit, 1, 1000)}";

        using var doc = await GetJsonAsync(url, ct);
        var result = new List<KlineDto>(doc.RootElement.GetArrayLength());
        foreach (var k in doc.RootElement.EnumerateArray())
        {
            // [openTime, open, high, low, close, volume, closeTime, quoteVolume, numberOfTrades, ...]
            result.Add(new KlineDto(
                FromUnixMs(k[0].GetInt64()),
                ParseDecimal(k[1]),
                ParseDecimal(k[2]),
                ParseDecimal(k[3]),
                ParseDecimal(k[4]),
                ParseDecimal(k[5]),
                ParseDecimal(k[7]),
                k[8].GetInt64(),
                ParseDecimal(k[9])));
        }

        return result;
    }

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new MarketDataUnavailableException($"Không kết nối được Binance: {ex.Message}", inner: ex);
        }

        using (response)
        {
            if (response.Headers.TryGetValues("X-MBX-USED-WEIGHT-1M", out var weight))
                logger.LogDebug("Binance used weight 1m: {Weight}", string.Join(",", weight));

            if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)418)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
                throw new MarketDataUnavailableException(
                    $"Binance rate limit ({(int)response.StatusCode}), thử lại sau {retryAfter}.", retryAfter);
            }

            if ((int)response.StatusCode >= 500)
                throw new MarketDataUnavailableException($"Binance lỗi {(int)response.StatusCode}.");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Binance từ chối request {(int)response.StatusCode}: {body}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
    }

    private static decimal ParseDecimal(JsonElement e) =>
        decimal.Parse(e.GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);

    private static DateTime FromUnixMs(long ms) => DateTime.UnixEpoch.AddMilliseconds(ms);
}
