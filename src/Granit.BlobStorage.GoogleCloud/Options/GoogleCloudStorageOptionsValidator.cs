using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.GoogleCloud.Options;

/// <summary>
/// Validates <see cref="GoogleCloudStorageOptions"/> at startup (fail-fast on missing configuration).
/// </summary>
internal sealed class GoogleCloudStorageOptionsValidator : IValidateOptions<GoogleCloudStorageOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, GoogleCloudStorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ProjectId))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ProjectId)} must be non-empty. " +
                "Set it to your GCP project ID (e.g. my-project-123).");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultBucket))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultBucket)} must be non-empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
