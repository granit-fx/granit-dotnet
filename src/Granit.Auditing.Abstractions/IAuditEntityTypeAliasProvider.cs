namespace Granit.Auditing;

/// <summary>
/// Resolves alternate CLR type names that audited entries may have been
/// stamped with on the audit log. Required when one logical entity is
/// persisted across multiple CLR types (e.g. <c>User</c> + <c>LocalIdentity</c>
/// + <c>FederatedIdentity</c> per ADR-051) so that read APIs can surface the
/// full audit history under the canonical name.
/// </summary>
/// <remarks>
/// <para>
/// The mapping is <strong>directional</strong> — <em>logical → physicals</em>.
/// A query for the logical name returns rows stamped with the logical name
/// itself <em>or</em> any of its registered physical aliases. A query for a
/// physical CLR name returns only rows for that exact name; forensics and
/// auth-only investigations keep their precision.
/// </para>
/// <para>
/// Register multiple implementations via <c>TryAddEnumerable</c>; consumers
/// take all providers and union the results. The default container is empty
/// (no aliases) when no provider is registered, which preserves the
/// pre-aliasing behaviour for hosts that don't use any split-persistence
/// aggregate.
/// </para>
/// </remarks>
public interface IAuditEntityTypeAliasProvider
{
    /// <summary>
    /// Returns the set of CLR type names that may appear on the audit log for
    /// rows whose canonical name is <paramref name="logicalEntityType"/>. The
    /// logical name itself is added by callers and must <em>not</em> be
    /// returned here. Returns an empty set when this provider has no aliases
    /// for the supplied type.
    /// </summary>
    IReadOnlySet<string> GetAliases(string logicalEntityType);
}
