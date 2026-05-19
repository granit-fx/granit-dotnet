using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Options;
using Granit.BlobStorage.Validators;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests.Validators;

public sealed class MagicBytesValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 2, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static BlobDescriptor MakeDescriptor(string contentType) =>
        BlobDescriptor.Create(
            id: Guid.NewGuid(),
            tenantId: TestTenantId,
            containerName: "prescriptions",
            objectKey: $"{TestTenantId}/prescriptions/2026/02/some-id",
            request: new BlobUploadRequest("file", contentType, 10_000_000L),
            createdAt: Now);

    private static BlobValidationContext MakeContext(BlobDescriptor descriptor, byte[] bytes) =>
        new()
        {
            Descriptor = descriptor,
            ActualSizeBytes = bytes.Length,
            OpenPartialStreamAsync = (_, _) =>
                Task.FromResult<Stream>(new MemoryStream(bytes)),
        };

    // ── MagicByteDetector unit tests ─────────────────────────────────────────

    [Fact]
    public void Detect_PdfSignature_ReturnsPdf()
    {
        byte[] buffer = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34]; // %PDF-1.4

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("application/pdf");
    }

    [Fact]
    public void Detect_JpegSignature_ReturnsJpeg()
    {
        byte[] buffer = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/jpeg");
    }

    [Fact]
    public void Detect_PngSignature_ReturnsPng()
    {
        byte[] buffer = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("image/png");
    }

    [Fact]
    public void Detect_DicomSignatureAtOffset128_ReturnsDicom()
    {
        byte[] buffer = new byte[132];
        // Pad to offset 128 then write DICM
        buffer[128] = 0x44; // D
        buffer[129] = 0x49; // I
        buffer[130] = 0x43; // C
        buffer[131] = 0x4D; // M

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("application/dicom");
    }

    [Fact]
    public void Detect_ZipSignature_ReturnsZip()
    {
        byte[] buffer = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBe("application/zip");
    }

    [Fact]
    public void Detect_UnknownBytes_ReturnsNull()
    {
        byte[] buffer = [0x00, 0x01, 0x02, 0x03, 0x04];

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBeNull();
    }

    [Fact]
    public void Detect_DicomBufferTooShort_DoesNotReturnDicom()
    {
        // Buffer shorter than 132 bytes: DICOM should not be detected
        byte[] buffer = new byte[131];
        buffer[128] = 0x44;
        buffer[129] = 0x49;
        buffer[130] = 0x43;

        string? result = MagicByteDetector.Detect(buffer);

        result.ShouldBeNull();
    }

    // ── MagicBytesValidator.ValidateAsync ────────────────────────────────────

    [Fact]
    public async Task ValidateAsync_PdfBytesWithPdfDeclared_ReturnsSuccess()
    {
        MagicBytesValidator validator = new(Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));
        byte[] pdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34];
        BlobValidationContext context = MakeContext(MakeDescriptor("application/pdf"), pdfBytes);

        BlobValidationResult result = await validator.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
        result.VerifiedContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public async Task ValidateAsync_JpegBytesDeclaredAsPdf_ReturnsFailure()
    {
        MagicBytesValidator validator = new(Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));
        byte[] jpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
        BlobValidationContext context = MakeContext(MakeDescriptor("application/pdf"), jpegBytes);

        BlobValidationResult result = await validator.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.FailureReason!.ShouldContain("image/jpeg");
        result.FailureReason!.ShouldContain("application/pdf");
    }

    [Fact]
    public async Task ValidateAsync_UnknownBytesWithAnyDeclaredType_PassesThroughWithDeclaredType()
    {
        MagicBytesValidator validator = new(Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));
        byte[] unknownBytes = new byte[50]; // all zeros — no known signature
        BlobDescriptor descriptor = MakeDescriptor("application/octet-stream");
        BlobValidationContext context = MakeContext(descriptor, unknownBytes);

        BlobValidationResult result = await validator.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue("unknown formats pass through to avoid false negatives");
        result.VerifiedContentType.ShouldBe("application/octet-stream");
    }

    [Fact]
    public async Task ValidateAsync_PngBytesCaseInsensitiveMatch_ReturnsSuccess()
    {
        MagicBytesValidator validator = new(Microsoft.Extensions.Options.Options.Create(new BlobStorageOptions()));
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
        // Declared in uppercase (unusual but should work)
        BlobValidationContext context = MakeContext(MakeDescriptor("IMAGE/PNG"), pngBytes);

        BlobValidationResult result = await validator.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_UnknownBytes_RejectUnverified_ReturnsFailure()
    {
        var opts = new Options.BlobStorageOptions { RejectUnverifiedContentTypes = true };
        MagicBytesValidator validator = new(Microsoft.Extensions.Options.Options.Create(opts));
        byte[] unknownBytes = new byte[50];
        BlobValidationContext context = MakeContext(MakeDescriptor("application/octet-stream"), unknownBytes);

        BlobValidationResult result = await validator.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.FailureReason!.ShouldContain("Unverified");
    }

    [Fact]
    public void Order_Is10() =>
        new MagicBytesValidator(Microsoft.Extensions.Options.Options.Create(new Options.BlobStorageOptions())).Order.ShouldBe(10);
}
