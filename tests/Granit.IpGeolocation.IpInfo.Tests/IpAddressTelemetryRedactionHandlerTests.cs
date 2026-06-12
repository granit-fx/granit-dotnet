using System.Diagnostics;
using System.Net;
using Granit.IpGeolocation.IpInfo.Internal;
using Shouldly;
using Xunit;

namespace Granit.IpGeolocation.IpInfo.Tests;

public sealed class IpAddressTelemetryRedactionHandlerTests
{
    private const string PublicIp = "8.8.8.8";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_RedactsClientIp_FromHttpClientActivityUrlTags()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using ActivitySource source = new("System.Net.Http");
        using Activity activity = source.StartActivity("System.Net.Http.HttpRequestOut")!;
        activity.SetTag("url.full", $"https://ipinfo.io/{PublicIp}/json");
        activity.SetTag("url.path", $"/{PublicIp}/json");

        await SendThroughHandlerAsync($"https://ipinfo.io/{PublicIp}/json");

        activity.GetTagItem("url.full").ShouldBe("https://ipinfo.io/redacted");
        activity.GetTagItem("url.path").ShouldBe("/redacted");
    }

    [Fact]
    public async Task SendAsync_RedactsClientIp_FromLegacyHttpUrlTag()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        using ActivitySource source = new("System.Net.Http");
        using Activity activity = source.StartActivity("System.Net.Http.HttpRequestOut")!;
        activity.SetTag("http.url", $"https://ipinfo.io/{PublicIp}/json");

        await SendThroughHandlerAsync($"https://ipinfo.io/{PublicIp}/json");

        activity.GetTagItem("http.url").ShouldBe("https://ipinfo.io/redacted");
    }

    [Fact]
    public async Task SendAsync_LeavesNonHttpClientActivityUntouched()
    {
        using ActivityListener listener = CreateListener();
        ActivitySource.AddActivityListener(listener);

        // Simulate an ambient inbound (ASP.NET Core) activity that must never be mutated by this handler.
        using ActivitySource source = new("Microsoft.AspNetCore");
        using Activity activity = source.StartActivity("inbound")!;
        activity.SetTag("url.full", $"https://app.example/{PublicIp}");

        await SendThroughHandlerAsync($"https://ipinfo.io/{PublicIp}/json");

        activity.GetTagItem("url.full").ShouldBe($"https://app.example/{PublicIp}");
    }

    private static ActivityListener CreateListener() => new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    };

    private static async Task SendThroughHandlerAsync(string requestUri)
    {
        using IpAddressTelemetryRedactionHandler handler = new() { InnerHandler = new OkHandler() };
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
