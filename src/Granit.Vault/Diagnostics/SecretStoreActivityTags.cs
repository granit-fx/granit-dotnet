namespace Granit.Vault.Diagnostics;

/// <summary>
/// Provider-agnostic <see cref="System.Diagnostics.Activity"/> tag keys used by
/// <c>ISecretStore</c> implementations. Provider-specific tags live on each
/// provider's own <c>Vault*ActivitySource.Tags</c> class.
/// </summary>
public static class SecretStoreActivityTags
{
    /// <summary>
    /// Canonical outcome tag. Matches the <c>outcome</c> dimension of the
    /// <c>granit.vault.secret.read</c> counter: <c>ok</c> | <c>not_found</c>
    /// | <c>denied</c> | <c>transient</c> | <c>error</c>.
    /// </summary>
    public const string Outcome = "secret.outcome";
}
