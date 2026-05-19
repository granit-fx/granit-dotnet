using Granit.Browsing.Playwright.Internal;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Playwright.Tests;

public sealed class PlaywrightPdfViewerCapabilityTests
{
    [Fact]
    public void Invalid_pdf_magic_throws_InvalidDataException()
    {
        Should.Throw<InvalidDataException>(
            () => PlaywrightPdfViewerCapability.ValidatePdfMagic([0x00, 0x01, 0x02, 0x03]));
    }

    [Fact]
    public void Truncated_pdf_throws_InvalidDataException()
    {
        Should.Throw<InvalidDataException>(
            () => PlaywrightPdfViewerCapability.ValidatePdfMagic([(byte)'%']));
    }

    [Fact]
    public void Valid_magic_does_not_throw()
    {
        byte[] magic = "%PDF-1.7\n"u8.ToArray();
        Should.NotThrow(() => PlaywrightPdfViewerCapability.ValidatePdfMagic(magic));
    }
}
