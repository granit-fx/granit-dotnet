namespace Granit.Vault;

/// <summary>
/// Identifies a secret to retrieve from <see cref="ISecretStore"/>.
/// </summary>
/// <param name="Name">
/// Provider-addressable secret name. Interpretation is provider-specific:
/// <list type="bullet">
///   <item>HashiCorp KV v2: relative path under the configured mount point (e.g. <c>"granit/mqtt/client-cert"</c>).</item>
///   <item>Azure Key Vault: secret name (e.g. <c>"mqtt-client-cert"</c>).</item>
///   <item>AWS Secrets Manager: secret name or ARN.</item>
///   <item>GCP Secret Manager: secret id (without <c>projects/.../secrets/...</c> prefix — the project is resolved from options).</item>
/// </list>
/// </param>
/// <param name="Version">
/// Optional version pin. When <c>null</c>, the latest available version is returned.
/// </param>
public sealed record SecretRequest(string Name, SecretVersion? Version = null)
{
    /// <summary>Builds a request for the latest version of <paramref name="name"/>.</summary>
    public static SecretRequest Latest(string name) => new(name);

    /// <summary>Builds a request for a specific <paramref name="version"/> of <paramref name="name"/>.</summary>
    public static SecretRequest At(string name, string version) =>
        new(name, new SecretVersion(version));
}
