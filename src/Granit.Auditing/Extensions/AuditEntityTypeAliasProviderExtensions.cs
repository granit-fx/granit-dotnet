namespace Granit.Auditing.Extensions;

/// <summary>
/// Helpers for unioning the contributions of multiple
/// <see cref="IAuditEntityTypeAliasProvider"/> instances.
/// </summary>
public static class AuditEntityTypeAliasProviderExtensions
{
    /// <summary>
    /// Resolves the full set of CLR type names a read query should match for
    /// the supplied canonical name. The returned set always contains the
    /// logical name itself plus every alias contributed by any provider.
    /// </summary>
    public static IReadOnlySet<string> Resolve(
        this IEnumerable<IAuditEntityTypeAliasProvider> providers,
        string logicalEntityType)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentException.ThrowIfNullOrEmpty(logicalEntityType);

        HashSet<string> result = new(StringComparer.Ordinal) { logicalEntityType };
        foreach (IAuditEntityTypeAliasProvider provider in providers)
        {
            foreach (string alias in provider.GetAliases(logicalEntityType))
            {
                result.Add(alias);
            }
        }
        return result;
    }
}
