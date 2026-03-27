using Granit.BlobStorage.Options;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.AzureBlob.Options;

/// <summary>
/// Configuration for the Azure Blob Storage provider.
/// Extends <see cref="BlobStorageOptions"/> with Azure-specific connection settings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Connection string</b>: set <see cref="ConnectionString"/> for development or
/// when Managed Identity is not available.
/// </para>
/// <para>
/// <b>Managed Identity</b>: set <see cref="UseManagedIdentity"/> to <c>true</c> and
/// <see cref="ServiceUri"/> to your storage account URI. <c>DefaultAzureCredential</c>
/// will handle authentication via Azure AD.
/// </para>
/// <para>
/// Credentials must be injected from <c>Granit.Vault</c> in production.
/// Never hardcode <see cref="ConnectionString"/> in source code or <c>appsettings.json</c>.
/// </para>
/// </remarks>
public sealed class AzureBlobOptions : BlobStorageOptions
{
    /// <summary>
    /// Azure Storage connection string. Used when <see cref="UseManagedIdentity"/> is <c>false</c>.
    /// Inject from Granit.Vault; never hardcode.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Default Azure Blob container name.
    /// </summary>
    public string DefaultContainer { get; set; } = string.Empty;

    /// <summary>
    /// When <c>true</c>, uses <c>DefaultAzureCredential</c> (Managed Identity) instead of
    /// <see cref="ConnectionString"/>. Requires <see cref="ServiceUri"/> to be set.
    /// Defaults to <c>false</c>.
    /// </summary>
    public bool UseManagedIdentity { get; set; }

    /// <summary>
    /// Azure Blob Storage account URI (e.g. <c>https://myaccount.blob.core.windows.net</c>).
    /// Required when <see cref="UseManagedIdentity"/> is <c>true</c>.
    /// </summary>
    public Uri? ServiceUri { get; set; }

    /// <summary>
    /// Multi-tenant isolation strategy. Defaults to <see cref="BlobTenantIsolation.Prefix"/>.
    /// </summary>
    public BlobTenantIsolation TenantIsolation { get; set; } = BlobTenantIsolation.Prefix;
}

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
        else
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.ConnectionString)} must be non-empty when {nameof(options.UseManagedIdentity)} is false. " +
                    "Inject from Granit.Vault.");
            }
        }

        if (string.IsNullOrWhiteSpace(options.DefaultContainer))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultContainer)} must be non-empty.");
        }

        return ValidateOptionsResult.Success;
    }
}
