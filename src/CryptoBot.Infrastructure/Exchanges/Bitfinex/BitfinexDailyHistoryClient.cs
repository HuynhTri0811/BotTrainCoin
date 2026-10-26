using System.Net;
using System.Text.Json;
using CryptoBot.Application.Abstractions;

namespace CryptoBot.Infrastructure.Exchanges.Bitfinex;

public sealed class BitfinexOptions
{
    /// <summary>REST base URL công khai (không cần API key).</summary>
    public string BaseUrl { get; set; } = "https://api-pub.bitfinex.com";
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
}

/// <summary>
/// Nến ngày công khai của Bitfinex: có BTC từ 2013, ETH từ 03/2016, ETC từ 07/2016 (Binance chỉ từ 2017).
/// Không trả số giao dịch/khối lượng mua chủ động. Ngày không có giao dịch bị bỏ qua.
/// Tham khảo: https://docs.bitfinex.com/reference/rest-public-candles
/// </summary>
public sealed class BitfinexDailyHistoryClient(HttpClient http) : IDailyHistoryClient
{
    public string Exchange => "Bitfinex";

    public async Task<IReadOnlyList<KlineDto>> GetDailyKlinesAsync(string symbol, DateTime startTimeUtc, int limit,
        CancellationToken ct)
    {
        var startMs = new DateTimeOffset(startTimeUtc).ToUnixTimeMilliseconds();
        var url = $"/v2/candles/trade:1D:t{Uri.EscapeDataString(symbol)}/hist" +
                  $"?start={startMs}&sort=1&limit={Math.Clamp(limit, 1, 10000)}";

        using var doc = await GetJsonAsync(url, ct);
        var result = new List<KlineDto>(doc.RootElement.GetArrayLength());
        foreach (var k in doc.RootElement.EnumerateArray())
        {
            // [MTS, OPEN, CLOSE, HIGH, LOW, VOLUME] — khác thứ tự OHLC của Binance.
            result.Add(new KlineDto(
                DateTime.UnixEpoch.AddMilliseconds(k[0].GetInt64()),
                Open: k[1].GetDecimal(),
                High: k[3].GetDecimal(),
                Low: k[4].GetDecimal(),
                Close: k[2].GetDecimal(),
                Volume: k[5].GetDecimal()));
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
            throw new MarketDataUnavailableException($"Không kết nối được Bitfinex: {ex.Message}", inner: ex);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
                throw new MarketDataUnavailableException($"Bitfinex rate limit, thử lại sau {retryAfter}.", retryAfter);
            }

            if ((int)response.StatusCode >= 500)
                throw new MarketDataUnavailableException($"Bitfinex lỗi {(int)response.StatusCode}.");

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"Bitfinex từ chối request {(int)response.StatusCode}: {body}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            // Lỗi nghiệp vụ trả về dạng ["error", code, message] với HTTP 200.
            if (doc.RootElement.GetArrayLength() > 0 && doc.RootElement[0].ValueKind == JsonValueKind.String)
            {
                var error = doc.RootElement.GetRawText();
                doc.Dispose();
                throw new MarketDataUnavailableException($"Bitfinex lỗi: {error}");
            }

            return doc;
        }
    }
}
