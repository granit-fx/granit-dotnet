using Granit.BlobStorage.Options;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Validators;

/// <summary>
/// Validates the actual content type of an uploaded blob against its declared MIME type
/// by reading the binary magic-byte signature from S3 (range GET, no full-file download).
/// </summary>
/// <remarks>
/// <para>
/// Order: <b>10</b>. Should run before <see cref="MaxSizeValidator"/> (Order=20).
/// </para>
/// <para>
/// For recognised MIME types (PDF, JPEG, PNG, GIF, TIFF, DICOM, ZIP), validation fails
/// if the detected type differs from the declared type. For unrecognised types, the validator
/// passes through and sets <see cref="BlobValidationResult.VerifiedContentType"/> to the
/// declared type (conservative behaviour: do not reject what cannot be inspected).
/// </para>
/// <para>
/// ZIP-based container formats (OOXML, ODF, EPUB, JAR) share the PK magic signature with
/// plain ZIP archives. When magic bytes indicate <c>application/zip</c> and the declared
/// type is a known ZIP-based container, validation passes using the declared type.
/// </para>
/// </remarks>
public sealed class MagicBytesValidator(IOptions<BlobStorageOptions> options) : IBlobValidator
{
    // ZIP-based container formats whose first bytes are identical to a plain .zip archive.
    // Magic-byte detection returns "application/zip" for all of them, so a declared-vs-detected
    // mismatch would incorrectly reject valid files. Accept these when zip is detected.
    private static readonly HashSet<string> ZipCompatibleTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Office Open XML (.docx / .xlsx / .pptx and their template variants)
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.template",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.template",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        "application/vnd.openxmlformats-officedocument.presentationml.template",
        "application/vnd.openxmlformats-officedocument.presentationml.slideshow",
        // OpenDocument (.odt / .ods / .odp)
        "application/vnd.oasis.opendocument.text",
        "application/vnd.oasis.opendocument.spreadsheet",
        "application/vnd.oasis.opendocument.presentation",
        "application/vnd.oasis.opendocument.graphics",
        // Other common ZIP containers
        "application/epub+zip",
        "application/java-archive",
    };

    /// <inheritdoc/>
    public int Order => 10;

    /// <inheritdoc/>
    public async Task<BlobValidationResult> ValidateAsync(
        BlobValidationContext context,
        CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[MagicByteDetector.RequiredByteCount];

        await using Stream stream = await context.OpenPartialStreamAsync(
            MagicByteDetector.RequiredByteCount, cancellationToken).ConfigureAwait(false);

        int totalRead = 0;
        int read;
        while ((read = await stream.ReadAsync(
            buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += read;
        }

        string? detectedType = MagicByteDetector.Detect(buffer.AsSpan(0, totalRead));

        if (detectedType is null)
        {
            if (options.Value.RejectUnverifiedContentTypes)
            {
                return BlobValidationResult.Failure(
                    $"Cannot verify content type '{context.Descriptor.DeclaredContentType}' " +
                    "from magic bytes. Unverified content types are rejected by policy.");
            }

            return BlobValidationResult.Success(context.Descriptor.DeclaredContentType);
        }

        // ZIP-based container formats (OOXML, ODF, EPUB, JAR…) share the PK magic signature
        // with plain ZIP; return the declared type rather than the generic "application/zip".
        if (string.Equals(detectedType, "application/zip", StringComparison.OrdinalIgnoreCase)
            && ZipCompatibleTypes.Contains(context.Descriptor.DeclaredContentType))
        {
            return BlobValidationResult.Success(context.Descriptor.DeclaredContentType);
        }

        if (!string.Equals(detectedType, context.Descriptor.DeclaredContentType,
            StringComparison.OrdinalIgnoreCase))
        {
            return BlobValidationResult.Failure(
                $"Content-Type mismatch: declared '{context.Descriptor.DeclaredContentType}' " +
                $"but magic bytes indicate '{detectedType}'. File rejected.");
        }

        return BlobValidationResult.Success(detectedType);
    }
}
