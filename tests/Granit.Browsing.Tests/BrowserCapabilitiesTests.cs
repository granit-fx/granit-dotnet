using Granit.Browsing;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests;

public sealed class BrowserCapabilitiesTests
{
    [Fact]
    public void None_should_be_zero() =>
        ((int)BrowserCapabilities.None).ShouldBe(0);

    [Fact]
    public void Flags_should_be_powers_of_two_and_unique()
    {
        BrowserCapabilities[] flags =
        [
            BrowserCapabilities.Screenshot,
            BrowserCapabilities.PdfGeneration,
            BrowserCapabilities.NetworkInterception,
            BrowserCapabilities.JavaScriptInjection,
            BrowserCapabilities.EmulateDevice,
            BrowserCapabilities.EmulateMedia,
            BrowserCapabilities.Geolocation,
            BrowserCapabilities.PdfViewerNative,
            BrowserCapabilities.AccessibilityTree,
            BrowserCapabilities.HarRecording,
            BrowserCapabilities.TraceRecording,
        ];

        // Power-of-two check.
        foreach (BrowserCapabilities flag in flags)
        {
            int v = (int)flag;
            (v & (v - 1)).ShouldBe(0, $"Flag {flag} is not a power of two.");
        }

        // Uniqueness — combined OR equals the arithmetic sum when all flags are disjoint.
        int combined = 0;
        int sum = 0;
        foreach (BrowserCapabilities flag in flags)
        {
            combined |= (int)flag;
            sum += (int)flag;
        }
        combined.ShouldBe(sum);
    }

    [Theory]
    [InlineData(BrowserCapabilities.Screenshot, BrowserCapabilities.Screenshot, true)]
    [InlineData(BrowserCapabilities.Screenshot | BrowserCapabilities.PdfGeneration, BrowserCapabilities.PdfGeneration, true)]
    [InlineData(BrowserCapabilities.Screenshot, BrowserCapabilities.PdfGeneration, false)]
    [InlineData(BrowserCapabilities.Screenshot | BrowserCapabilities.PdfGeneration,
        BrowserCapabilities.Screenshot | BrowserCapabilities.PdfGeneration | BrowserCapabilities.TraceRecording, false)]
    public void HasFlag_should_match_combined_capability_check(
        BrowserCapabilities advertised, BrowserCapabilities required, bool expected)
        => advertised.HasFlag(required).ShouldBe(expected);
}
