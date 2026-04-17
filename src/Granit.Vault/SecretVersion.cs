namespace Granit.Vault;

/// <summary>
/// Identifies a specific version of a secret. Format is provider-specific:
/// integer (HashiCorp, GCP), UUID (Azure, AWS), or the literal <c>"latest"</c>.
/// </summary>
/// <param name="Identifier">Provider-specific version identifier.</param>
public sealed record SecretVersion(string Identifier);
