using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.Azure.Options;

/// <summary>Configuration for Azure Key Vault encryption and secrets.</summary>
public sealed class AzureKeyVaultOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:Azure";

    /// <summary>Azure Key Vault URI (e.g. "https://my-vault.vault.azure.net/"). Required.</summary>
    [Required]
    public string VaultUri { get; set; } = string.Empty;

    /// <summary>Key name in Azure Key Vault used for transit encryption. Required.</summary>
    [Required]
    public string EncryptionKeyName { get; set; } = string.Empty;

    /// <summary>
    /// Encryption algorithm to use. Default: "RSA-OAEP-256".
    /// Supported values: "RSA-OAEP", "RSA-OAEP-256".
    /// </summary>
    public string EncryptionAlgorithm { get; set; } = "RSA-OAEP-256";

    /// <summary>Secret name in Azure Key Vault for database credentials (JSON with "username" and "password" fields). Optional.</summary>
    public string? DatabaseSecretName { get; set; }

    /// <summary>Interval in minutes between rotation checks for database credentials. Default: 5.</summary>
    [Range(1, 1440)]
    public double RotationCheckIntervalMinutes { get; set; } = 5;

    /// <summary>API call timeout in seconds. Default: 30.</summary>
    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;
}
