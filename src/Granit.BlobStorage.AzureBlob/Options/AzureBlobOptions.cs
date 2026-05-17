using Granit.BlobStorage.Options;

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
