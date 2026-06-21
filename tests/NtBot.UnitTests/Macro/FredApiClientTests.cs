using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NtBot.Macro.Configuration;
using NtBot.Macro.Providers.Fred;

namespace NtBot.UnitTests.Macro;

public class FredApiClientTests
{
    [Fact]
    public async Task GetLatestObservationAsync_ReturnsNull_WhenApiKeyMissing()
    {
        var client = CreateClient("", _ => new HttpResponseMessage(HttpStatusCode.OK));

        var result = await client.GetLatestObservationAsync(FredSeries.Vix);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestObservationAsync_ParsesValidResponse()
    {
        const string json = """
            {
              "observations": [
                { "date": "2026-06-20", "value": "18.42" }
              ]
            }
            """;

        var client = CreateClient("test-key", _ => JsonResponse(json));

        var result = await client.GetLatestObservationAsync(FredSeries.Vix);

        Assert.NotNull(result);
        Assert.Equal(FredSeries.Vix, result!.SeriesId);
        Assert.Equal(18.42m, result.Value);
        Assert.Equal(new DateTime(2026, 6, 20), result.Date);
    }

    [Fact]
    public async Task GetLatestObservationAsync_SkipsMissingValues()
    {
        const string json = """
            {
              "observations": [
                { "date": "2026-06-20", "value": "." },
                { "date": "2026-06-19", "value": "17.10" }
              ]
            }
            """;

        var client = CreateClient("test-key", _ => JsonResponse(json));

        var result = await client.GetLatestObservationAsync(FredSeries.Vix);

        Assert.NotNull(result);
        Assert.Equal(17.10m, result!.Value);
        Assert.Equal(new DateTime(2026, 6, 19), result.Date);
    }

    [Fact]
    public async Task GetLatestObservationAsync_ReturnsNull_OnHttpFailure()
    {
        var client = CreateClient("test-key", _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await client.GetLatestObservationAsync(FredSeries.Us10Y);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestObservationAsync_IncludesSeriesIdAndApiKeyInRequest()
    {
        HttpRequestMessage? captured = null;
        var client = CreateClient("secret-key", request =>
        {
            captured = request;
            return JsonResponse("""{"observations":[{"date":"2026-01-01","value":"4.25"}]}""");
        });

        await client.GetLatestObservationAsync(FredSeries.FedFunds);

        Assert.NotNull(captured);
        var uri = captured!.RequestUri!.ToString();
        Assert.Contains("series_id=FEDFUNDS", uri, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("api_key=secret-key", uri, StringComparison.OrdinalIgnoreCase);
    }

    private static FredApiClient CreateClient(string apiKey, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.stlouisfed.org/fred/") };
        var options = Options.Create(new MacroOptions { FredApiKey = apiKey });
        return new FredApiClient(http, options, NullLogger<FredApiClient>.Instance);
    }

    private static HttpResponseMessage JsonResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
