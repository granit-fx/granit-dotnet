namespace Granit.Auditing;

/// <summary>
/// Dictionary-backed <see cref="IAuditEntityTypeAliasProvider"/> for modules
/// that own a known set of split-persistence aliases at compile time.
/// </summary>
/// <remarks>
/// Use ordinal string comparison: audit-log <c>EntityType</c> values are CLR
/// type names, which are case-sensitive.
/// </remarks>
public sealed class StaticAuditEntityTypeAliasProvider : IAuditEntityTypeAliasProvider
{
    private static readonly IReadOnlySet<string> Empty = new HashSet<string>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _aliases;

    /// <summary>
    /// Creates a provider backed by the supplied logical-to-physicals map.
    /// </summary>
    /// <param name="aliases">
    /// Keys are canonical names exposed by readers; values are the CLR type
    /// names the audit log may have stamped for that canonical entity.
    /// </param>
    public StaticAuditEntityTypeAliasProvider(IReadOnlyDictionary<string, IReadOnlySet<string>> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        _aliases = aliases;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> GetAliases(string logicalEntityType) =>
        _aliases.TryGetValue(logicalEntityType, out IReadOnlySet<string>? aliases)
            ? aliases
            : Empty;
}
