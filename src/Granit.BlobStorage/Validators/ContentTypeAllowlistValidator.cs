using Granit.BlobStorage.Options;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Validators;

/// <summary>
/// Rejects blobs whose declared content type is not in the configured allowlist.
/// Only active when <see cref="BlobStorageOptions.AllowedContentTypes"/> is non-empty.
/// </summary>
/// <remarks>
/// Order: <b>5</b>. Runs before <see cref="MagicBytesValidator"/> (Order=10) to fail fast
/// on disallowed types without reading any bytes from the storage provider.
/// </remarks>
public sealed class ContentTypeAllowlistValidator(IOptions<BlobStorageOptions> options) : IBlobValidator
{
    /// <inheritdoc/>
    public int Order => 5;

    /// <inheritdoc/>
    public Task<BlobValidationResult> ValidateAsync(
        BlobValidationContext context,
        CancellationToken cancellationToken = default)
    {
        HashSet<string> allowed = options.Value.AllowedContentTypes;

        if (allowed.Count == 0)
        {
            return Task.FromResult(BlobValidationResult.Success(context.Descriptor.DeclaredContentType));
        }

        if (allowed.Contains(context.Descriptor.DeclaredContentType, StringComparer.OrdinalIgnoreCase))
        {
            return Task.FromResult(BlobValidationResult.Success(context.Descriptor.DeclaredContentType));
        }

        return Task.FromResult(BlobValidationResult.Failure(
            $"Content type '{context.Descriptor.DeclaredContentType}' is not allowed. " +
            $"Accepted types: {string.Join(", ", allowed)}."));
    }
}
