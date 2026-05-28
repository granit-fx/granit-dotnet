using System.Diagnostics;

namespace Granit.Vault.Azure.Diagnostics;

/// <summary>OpenTelemetry activity source for the Azure Key Vault provider.</summary>
internal static class VaultAzureActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Vault.Azure";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string AkvEncrypt = "akv.encrypt";
        public const string AkvDecrypt = "akv.decrypt";
        public const string AkvGetKey = "akv.get-key";
        public const string AkvMac = "akv.mac";
        public const string AkvVerify = "akv.verify";
#pragma warning disable GRSEC003 // Operation names, not secrets
        public const string AkvGetSecret = "akv.get-secret";
        public const string AkvCheckRotation = "akv.check-rotation";
#pragma warning restore GRSEC003
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string KeyName = "akv.key_name";
        public const string VaultUri = "akv.vault_uri";
    }
}
