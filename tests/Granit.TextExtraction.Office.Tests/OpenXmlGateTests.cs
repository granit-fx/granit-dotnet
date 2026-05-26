using Granit.TextExtraction.Office.Internal;
using Shouldly;
using Xunit;
using ExtractionOptions = Granit.TextExtraction.Options.GranitTextExtractionOptions;

namespace Granit.TextExtraction.Office.Tests;

/// <summary>
/// Unit-level coverage of <see cref="OpenXmlGate"/> — the security gate that screens every
/// .docx/.xlsx/.pptx package before OpenXml gets a chance to allocate. The Word/Excel/PowerPoint
/// extractor tests exercise the gate end-to-end; these focused tests pin individual branches
/// of <see cref="OpenXmlGate.GateResult"/>.
/// </summary>
public sealed class OpenXmlGateTests
{
    [Fact]
    public void Inspect_returns_TooManyEntries_when_archive_exceeds_MaxZipEntries()
    {
        byte[] package = OfficeFixtures.ZipWithEntryCount(entries: 10);
        ExtractionOptions options = new() { MaxZipEntries = 5 };

        OpenXmlGate.Inspect(package, options).ShouldBe(OpenXmlGate.GateResult.TooManyEntries);
    }

    [Fact]
    public void Inspect_returns_TooLargeDecompressed_when_cumulative_size_exceeds_cap()
    {
        // Real bytes, modest ratio — total declared length exceeds the cap.
        byte[] package = OfficeFixtures.ZipWithAdvertisedSize(advertisedBytes: 8 * 1024);
        ExtractionOptions options = new() { MaxDecompressedBytes = 1024 };

        OpenXmlGate.Inspect(package, options).ShouldBe(OpenXmlGate.GateResult.TooLargeDecompressed);
    }

    [Fact]
    public void Inspect_returns_SuspiciousCompressionRatio_on_high_ratio_payloads()
    {
        // 8 MB of zeros lands well above the 1 KB compressed-size floor of the
        // ratio gate and still compresses with a ratio comfortably above 200× — the gate
        // must catch this before OpenXml streams the local-header bytes.
        byte[] package = OfficeFixtures.ZipWithHighCompressionRatio(rawSize: 8 * 1024 * 1024);
        ExtractionOptions options = new()
        {
            MaxZipEntries = 100,
            // 1 GB cap — well above 8 MB so the cumulative-size branch never trips,
            // isolating this test on the ratio branch only.
            MaxDecompressedBytes = 1024L * 1024 * 1024,
        };

        OpenXmlGate.Inspect(package, options).ShouldBe(OpenXmlGate.GateResult.SuspiciousCompressionRatio);
    }

    [Fact]
    public void Inspect_accepts_normal_docx_packages()
    {
        byte[] package = OfficeFixtures.Docx("Hello world.");
        ExtractionOptions options = new();

        OpenXmlGate.Inspect(package, options).ShouldBe(OpenXmlGate.GateResult.Ok);
    }

    [Fact]
    public void Inspect_returns_InvalidPackage_on_garbage_input()
    {
        byte[] package = OfficeFixtures.InvalidZip();
        ExtractionOptions options = new();

        OpenXmlGate.Inspect(package, options).ShouldBe(OpenXmlGate.GateResult.InvalidPackage);
    }
}
