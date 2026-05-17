using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.S3.Options;

/// <summary>
/// Validates <see cref="S3BlobOptions"/> at startup (fail-fast on missing credentials).
/// </summary>
internal sealed class S3BlobOptionsValidator : IValidateOptions<S3BlobOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, S3BlobOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ServiceUrl)} must be non-empty. " +
                "Set it to your S3-compatible endpoint (e.g. https://s3.eu-west-1.amazonaws.com or http://localhost:9000).");
        }

        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out Uri? serviceUri))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ServiceUrl)} must be a valid absolute URI.");
        }

        // Allow HTTP only for localhost (MinIO development). Enforce HTTPS for all other hosts.
        bool isLocalhost = serviceUri.Host is "localhost" or "127.0.0.1" or "::1";
        if (serviceUri.Scheme != Uri.UriSchemeHttps && !isLocalhost)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ServiceUrl)} must use HTTPS for non-localhost endpoints. " +
                "HTTP is only allowed for local development (localhost/127.0.0.1).");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.AccessKey)} must be non-empty. Inject from Granit.Vault.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.SecretKey)} must be non-empty. Inject from Granit.Vault.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultBucket))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultBucket)} must be non-empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
