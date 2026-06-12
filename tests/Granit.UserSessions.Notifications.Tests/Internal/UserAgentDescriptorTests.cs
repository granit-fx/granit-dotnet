using Granit.UserSessions.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Internal;

/// <summary>
/// Coarse browser-family and OS-family extraction, returned <b>separately</b> (never joined in code)
/// so the email template composes and labels them per culture. Pins the common desktop/mobile
/// combinations and the order-sensitive cases (Edge/Opera carrying "Chrome", Chrome carrying
/// "Safari", ChromeOS carrying "Linux"); browser-only / OS-only / null degrade gracefully.
/// </summary>
public sealed class UserAgentDescriptorTests
{
    [Theory]
    // Chrome on Windows
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36",
        "Chrome", "Windows")]
    // Edge on Windows (UA also contains Chrome + Safari → Edge must win)
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 Edg/120.0",
        "Edge", "Windows")]
    // Firefox on macOS
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Firefox", "macOS")]
    // Safari on iPhone (Chrome ruled out)
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
        "Safari", "iPhone")]
    // Safari on iPad
    [InlineData(
        "Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
        "Safari", "iPad")]
    // Chrome on Android
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36",
        "Chrome", "Android")]
    // Opera on Windows (carries Chrome)
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 OPR/106.0",
        "Opera", "Windows")]
    // Samsung Internet on Android (carries Chrome)
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/23.0 Chrome/115.0 Mobile Safari/537.36",
        "Samsung Internet", "Android")]
    // Chrome on iOS reports CriOS
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/120.0 Mobile/15E148 Safari/604.1",
        "Chrome", "iPhone")]
    // ChromeOS carries "Linux" → ChromeOS must win
    [InlineData(
        "Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36",
        "Chrome", "ChromeOS")]
    // Firefox on Linux
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Firefox", "Linux")]
    public void ReturnsBrowserAndOperatingSystemSeparately(string userAgent, string expectedBrowser, string expectedOs)
    {
        UserAgentDescriptor.Browser(userAgent).ShouldBe(expectedBrowser);
        UserAgentDescriptor.OperatingSystem(userAgent).ShouldBe(expectedOs);
    }

    [Theory]
    // Developer / API clients have no OS token.
    [InlineData("PostmanRuntime/7.36.0", "Postman")]
    [InlineData("curl/8.4.0", "cURL")]
    public void Browser_DeveloperClients(string userAgent, string expectedBrowser) =>
        UserAgentDescriptor.Browser(userAgent).ShouldBe(expectedBrowser);

    [Fact]
    public void Browser_UnknownBrowser_ReturnsNull() =>
        UserAgentDescriptor.Browser("SomeCrawler/1.0 (Windows NT 10.0)").ShouldBeNull();

    [Fact]
    public void OperatingSystem_UnknownOs_ReturnsNull() =>
        UserAgentDescriptor.OperatingSystem("Chrome/120.0 (some-exotic-device)").ShouldBeNull();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("totally-unrecognisable-token")]
    public void BlankOrUnrecognised_ReturnsNull(string? userAgent)
    {
        UserAgentDescriptor.Browser(userAgent).ShouldBeNull();
        UserAgentDescriptor.OperatingSystem(userAgent).ShouldBeNull();
    }
}
