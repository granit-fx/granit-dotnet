using Granit.UserSessions.Notifications.Internal;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Internal;

/// <summary>
/// Coarse "Browser on OS" labelling. Pins the common desktop/mobile combinations and the graceful
/// degradation paths (browser-only, OS-only, null). Order-sensitive cases (Edge/Opera carrying
/// "Chrome", Chrome carrying "Safari", ChromeOS carrying "Linux") are covered explicitly.
/// </summary>
public sealed class UserAgentDescriptorTests
{
    [Theory]
    // Chrome on Windows
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36",
        "Chrome on Windows")]
    // Edge on Windows (UA also contains Chrome + Safari → Edge must win)
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 Edg/120.0",
        "Edge on Windows")]
    // Firefox on macOS
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Firefox on macOS")]
    // Safari on iPhone (Chrome ruled out)
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1",
        "Safari on iPhone")]
    // Chrome on Android
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36",
        "Chrome on Android")]
    // Opera on Windows (carries Chrome)
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 OPR/106.0",
        "Opera on Windows")]
    // Chrome on iOS reports CriOS
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/120.0 Mobile/15E148 Safari/604.1",
        "Chrome on iPhone")]
    // ChromeOS carries "Linux" → ChromeOS must win
    [InlineData(
        "Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36",
        "Chrome on ChromeOS")]
    // Firefox on Linux
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64; rv:121.0) Gecko/20100101 Firefox/121.0",
        "Firefox on Linux")]
    public void Describe_ReturnsBrowserOnOs(string userAgent, string expected) =>
        UserAgentDescriptor.Describe(userAgent).ShouldBe(expected);

    [Fact]
    public void Describe_UnknownBrowser_DegradesToOsOnly() =>
        UserAgentDescriptor.Describe("SomeCrawler/1.0 (Windows NT 10.0)").ShouldBe("Windows");

    [Fact]
    public void Describe_UnknownOs_DegradesToBrowserOnly() =>
        UserAgentDescriptor.Describe("Chrome/120.0 (some-exotic-device)").ShouldBe("Chrome");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("totally-unrecognisable-token")]
    public void Describe_BlankOrUnrecognised_ReturnsNull(string? userAgent) =>
        UserAgentDescriptor.Describe(userAgent).ShouldBeNull();
}
