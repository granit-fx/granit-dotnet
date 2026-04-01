using System.Collections.Frozen;

namespace Granit.Templating.Layouts.Internal;

/// <summary>
/// Default implementation of <see cref="ILayoutRegistry"/>.
/// Built once at DI resolution from all registered <see cref="LayoutRegistration"/> instances.
/// </summary>
/// <remarks>
/// Uses two lookup structures:
/// <list type="bullet">
///   <item><see cref="FrozenDictionary{TKey,TValue}"/> for exact matches (O(1))</item>
///   <item>Sorted list for prefix patterns (descending priority, then specificity)</item>
/// </list>
/// Exact matches always take precedence over prefix matches.
/// </remarks>
internal sealed class LayoutRegistry : ILayoutRegistry
{
    private readonly FrozenDictionary<string, string> _exactMatches;
    private readonly IReadOnlyList<(string Prefix, string LayoutName)> _prefixMatches;
    private readonly IReadOnlyList<string> _allLayoutNames;

    internal LayoutRegistry(IEnumerable<LayoutRegistration> registrations)
    {
        Dictionary<string, string> exact = new(StringComparer.Ordinal);
        List<(string Prefix, string LayoutName, int Priority)> prefixes = [];

        foreach (LayoutRegistration reg in registrations)
        {
            if (reg.Pattern.EndsWith(".*", StringComparison.Ordinal))
            {
                string prefix = reg.Pattern[..^2]; // strip ".*"
                prefixes.Add((prefix, reg.LayoutTemplateName, reg.Priority));
            }
            else
            {
                exact[reg.Pattern] = reg.LayoutTemplateName;
            }
        }

        _exactMatches = exact.ToFrozenDictionary(StringComparer.Ordinal);

        _prefixMatches = prefixes
            .OrderByDescending(p => p.Priority)
            .ThenByDescending(p => p.Prefix.Length)
            .Select(p => (p.Prefix, p.LayoutName))
            .ToList();

        _allLayoutNames = exact.Values
            .Concat(prefixes.Select(p => p.LayoutName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public string? GetLayoutName(string templateName)
    {
        // 1. Exact match always wins
        if (_exactMatches.TryGetValue(templateName, out string? layout))
        {
            return layout;
        }

        // 2. Prefix match (first match wins — sorted by priority, then specificity)
        foreach ((string prefix, string layoutName) in _prefixMatches)
        {
            if (templateName.StartsWith(prefix + ".", StringComparison.Ordinal)
                || templateName.Equals(prefix, StringComparison.Ordinal))
            {
                return layoutName;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetAllLayoutNames() => _allLayoutNames;
}
