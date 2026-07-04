using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AzureBlob.Options;

/// <summary>
/// Validates <see cref="AzureBlobOptions"/> at startup (fail-fast on misconfiguration).
/// </summary>
internal sealed class AzureBlobOptionsValidator : IValidateOptions<AzureBlobOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AzureBlobOptions options)
    {
        if (options.UseManagedIdentity)
        {
            if (options.ServiceUri is null)
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.ServiceUri)} must be set when {nameof(options.UseManagedIdentity)} is true.");
            }

            if (options.ServiceUri.Scheme != Uri.UriSchemeHttps)
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.ServiceUri)} must use HTTPS.");
            }
        }
        else if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ConnectionString)} must be non-empty when {nameof(options.UseManagedIdentity)} is false. " +
                "Inject from Granit.Vault.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultContainer)} must be non-empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
