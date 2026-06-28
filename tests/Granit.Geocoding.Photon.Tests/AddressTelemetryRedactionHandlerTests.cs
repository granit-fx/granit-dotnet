using System.Diagnostics;
using System.Net;
using Granit.Geocoding.Photon.Internal;
using Shouldly;
using Xunit;

namespace Granit.Geocoding.Photon.Tests;

public sealed class AddressTelemetryRedactionHandlerTests
{
    private const string AddressQuery = "api?q=Rue%20de%20la%20Loi%2C%20Brussels%2C%20BE&limit=1";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_RedactsAddress_FromHttpClientActivityUrlTags()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using ActivitySource source = new("System.Net.Http");
        using Activity activity = source.StartActivity("System.Net.Http.HttpRequestOut")!;
        activity.SetTag("url.full", $"https://photon.komoot.io/{AddressQuery}");
        activity.SetTag("url.query", "q=Rue%20de%20la%20Loi%2C%20Brussels%2C%20BE&limit=1");

        await SendThroughHandlerAsync($"https://photon.komoot.io/{AddressQuery}");

        activity.GetTagItem("url.full").ShouldBe("https://photon.komoot.io/api?redacted");
        activity.GetTagItem("url.query").ShouldBe("redacted");
    }

    [Fact]
    public async Task SendAsync_RedactsAddress_FromLegacyHttpUrlTag()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using ActivitySource source = new("System.Net.Http");
        using Activity activity = source.StartActivity("System.Net.Http.HttpRequestOut")!;
        activity.SetTag("http.url", $"https://photon.komoot.io/{AddressQuery}");

        await SendThroughHandlerAsync($"https://photon.komoot.io/{AddressQuery}");

        activity.GetTagItem("http.url").ShouldBe("https://photon.komoot.io/api?redacted");
    }

    [Fact]
    public async Task SendAsync_LeavesNonHttpClientActivityUntouched()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        // Simulate an ambient inbound (ASP.NET Core) activity that must never be mutated by this handler.
        using ActivitySource source = new("Microsoft.AspNetCore");
        using Activity activity = source.StartActivity("inbound")!;
        activity.SetTag("url.full", $"https://app.example/{AddressQuery}");

        await SendThroughHandlerAsync($"https://photon.komoot.io/{AddressQuery}");

        activity.GetTagItem("url.full").ShouldBe($"https://app.example/{AddressQuery}");
    }

    private static ActivityListener CreateListener() => new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    private static async Task SendThroughHandlerAsync(string requestUri)
    {
        using AddressTelemetryRedactionHandler handler = new() { InnerHandler = new OkHandler() };
        using HttpMessageInvoker invoker = new(handler);
        using HttpRequestMessage request = new(HttpMethod.Get, requestUri);
        using HttpResponseMessage response = await invoker.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class OkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}
