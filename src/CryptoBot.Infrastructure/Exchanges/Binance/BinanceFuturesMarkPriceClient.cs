using System.Globalization;
using System.Net;
using System.Text.Json;
using CryptoBot.Application.Abstractions;
using CryptoBot.Application.Futures;

namespace CryptoBot.Infrastructure.Exchanges.Binance;

/// <summary>
/// Mark price USDⓈ-M Futures qua REST công khai (không cần API key), chỉ đọc.
/// Endpoint: GET /fapi/v1/premiumIndex.
/// </summary>
public sealed class BinanceFuturesMarkPriceClient(HttpClient http) : IMarkPriceClient
{
    public async Task<MarkPrice> GetMarkPriceAsync(string symbol, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync($"/fapi/v1/premiumIndex?symbol={Uri.EscapeDataString(symbol)}", ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new MarketDataUnavailableException($"Không kết nối được Binance Futures: {ex.Message}", inner: ex);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.TooManyRequests or (HttpStatusCode)418)
                throw new MarketDataUnavailableException("Binance Futures rate limit.",
                    response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1));
            if (!response.IsSuccessStatusCode)
                throw new MarketDataUnavailableException($"Binance Futures lỗi {(int)response.StatusCode}.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            return new MarkPrice(
                symbol,
                decimal.Parse(root.GetProperty("markPrice").GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture),
                DateTime.UnixEpoch.AddMilliseconds(root.GetProperty("time").GetInt64()));
        }
    }
}
