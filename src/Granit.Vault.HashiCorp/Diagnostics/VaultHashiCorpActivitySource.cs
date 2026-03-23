using System.Diagnostics;

namespace Granit.Vault.HashiCorp.Diagnostics;

/// <summary>OpenTelemetry activity source for the HashiCorp Vault provider.</summary>
internal static class VaultHashiCorpActivitySource
{
    /// <summary>Activity source name.</summary>
    public const string Name = "Granit.Vault.HashiCorp";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Operation names.</summary>
    internal static class Operations
    {
        public const string TransitEncrypt = "vault.transit.encrypt";
        public const string TransitDecrypt = "vault.transit.decrypt";
        public const string TransitRewrap = "vault.transit.rewrap";
#pragma warning disable GRSEC003 // Operation names, not secrets
        public const string DbObtainCredentials = "vault.db.obtain-credentials";
        public const string DbRenewLease = "vault.db.renew-lease";
#pragma warning restore GRSEC003
        public const string KvRead = "vault.kv.read";
        public const string KvWrite = "vault.kv.write";
        public const string KvDelete = "vault.kv.delete";
    }

    /// <summary>Tag names.</summary>
    internal static class Tags
    {
        public const string KeyName = "vault.key_name";
        public const string MountPoint = "vault.mount_point";
    }
}
