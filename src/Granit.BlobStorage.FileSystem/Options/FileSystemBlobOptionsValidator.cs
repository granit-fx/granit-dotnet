using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.FileSystem.Options;

/// <summary>
/// Validates <see cref="FileSystemBlobOptions"/> at startup (fail-fast on misconfiguration).
/// </summary>
internal sealed class FileSystemBlobOptionsValidator : IValidateOptions<FileSystemBlobOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, FileSystemBlobOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BasePath))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.BasePath)} must be non-empty. " +
                "Set it to the root directory for blob storage (e.g. ./blobs or /data/blobs).");
        }

        return ValidateOptionsResult.Success;
    }
}
