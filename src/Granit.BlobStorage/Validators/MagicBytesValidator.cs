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
/// </remarks>
public sealed class MagicBytesValidator(IOptions<BlobStorageOptions> options) : IBlobValidator
{
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
