using System.Diagnostics;

namespace Granit.Vault.Aws.Diagnostics;

/// <summary>OpenTelemetry activity source for the AWS Vault provider.</summary>
internal static class VaultAwsActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Vault.Aws";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string KmsEncrypt = "kms.encrypt";
        public const string KmsDecrypt = "kms.decrypt";
        public const string KmsDescribeKey = "kms.describe-key";
        public const string KmsMac = "kms.mac";
        public const string KmsVerify = "kms.verify";
#pragma warning disable GRSEC003 // Operation names, not secrets
        public const string SecretsObtain = "secrets.obtain";
        public const string SecretsCheck = "secrets.check-rotation";
#pragma warning restore GRSEC003
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string KeyName = "kms.key_name";
        public const string Region = "aws.region";
    }
}
