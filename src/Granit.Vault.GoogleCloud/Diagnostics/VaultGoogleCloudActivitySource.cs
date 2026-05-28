using System.Diagnostics;

namespace Granit.Vault.GoogleCloud.Diagnostics;

/// <summary>OpenTelemetry activity source for the Google Cloud Vault provider.</summary>
internal static class VaultGoogleCloudActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Vault.GoogleCloud";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string KmsEncrypt = "cloudkms.encrypt";
        public const string KmsDecrypt = "cloudkms.decrypt";
        public const string KmsGetKey = "cloudkms.get-key";
        public const string KmsMac = "cloudkms.mac";
        public const string KmsVerify = "cloudkms.verify";
#pragma warning disable GRSEC003 // Operation names, not secrets
        public const string SecretsObtain = "secretmanager.obtain";
        public const string SecretsCheck = "secretmanager.check-rotation";
#pragma warning restore GRSEC003
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string KeyName = "cloudkms.key_name";
        public const string ProjectId = "gcp.project_id";
    }
}
