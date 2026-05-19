using Granit.DataLookup.Descriptors;
using Granit.DataLookup.Registry;
using Microsoft.Extensions.Localization;

namespace Granit.DataLookup.Sources;

/// <summary>
/// Lookup source backed by a CLR <see langword="enum"/> type. Values are the enum names;
/// labels are resolved from a <see cref="IStringLocalizer"/> using the convention
/// <c>Enum:{TypeName}.{Value}</c>. Falls back to the enum name when no localized text
/// is registered.
/// </summary>
/// <remarks>
/// <para>
/// Rationale (ADR-023): enums in C# drive code paths (branches, SQL, serialization),
/// not data. Storing them in a reference-data table would invite administrators to add
/// values that the compiled binary cannot honor. Exposing them as a lookup source lets
/// admin UIs render localized pickers without turning the enum into a DB-editable list.
/// </para>
/// <para>
/// Zero database roundtrip: materialization is <c>Enum.GetValues</c> at construction
/// time; search is an in-memory filter on label + name.
/// </para>
/// </remarks>
/// <typeparam name="TEnum">The enum type to expose as a lookup.</typeparam>
public sealed class EnumLookupSource<TEnum> : ILookupSource, IKindProviderLookupSource
    where TEnum : struct, Enum
{
    private readonly IStringLocalizer _localizer;
    private readonly string[] _names;

    /// <summary>Initializes a new <see cref="EnumLookupSource{TEnum}"/> under <paramref name="name"/>.</summary>
    /// <param name="name">Unique registry key (e.g. <c>"enum-aggregation-type"</c>).</param>
    /// <param name="localizer">Localizer used to resolve labels.</param>
    /// <param name="requiredPermission">Optional permission required to invoke the source.</param>
    public EnumLookupSource(string name, IStringLocalizer localizer, string? requiredPermission = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(localizer);

        Name = name;
        _localizer = localizer;
        RequiredPermission = requiredPermission;
        _names = Enum.GetNames<TEnum>();
    }

    /// <inheritdoc/>
    public string Name { get; }

    /// <inheritdoc/>
    public string? RequiredPermission { get; }

    /// <inheritdoc/>
    public IReadOnlyList<string> ScopeKeys => [];

    LookupKind IKindProviderLookupSource.Kind => LookupKind.Enum;

    /// <inheritdoc/>
    public ValueTask<LookupResult> SearchAsync(LookupQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        List<LookupItem> items = new(_names.Length);
        string? search = query.Search?.Trim();

        foreach (string enumName in _names)
        {
            string label = ResolveLabel(enumName);

            if (!string.IsNullOrEmpty(search) &&
                !label.Contains(search, StringComparison.CurrentCultureIgnoreCase) &&
                !enumName.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            items.Add(new LookupItem(enumName, label));
        }

        int totalCount = items.Count;
        int skip = Math.Max(0, (query.Page - 1) * query.PageSize);
        LookupItem[] page = [.. items.Skip(skip).Take(Math.Max(1, query.PageSize))];

        return ValueTask.FromResult(new LookupResult(page, totalCount, ContinuationToken: null));
    }

    /// <inheritdoc/>
    public ValueTask<LookupItem?> ResolveByValueAsync(object value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        string? candidate = value.ToString();
        if (string.IsNullOrEmpty(candidate) || !Enum.TryParse<TEnum>(candidate, ignoreCase: true, out TEnum parsed))
        {
            return ValueTask.FromResult<LookupItem?>(null);
        }

        string canonical = parsed.ToString();
        return ValueTask.FromResult<LookupItem?>(new LookupItem(canonical, ResolveLabel(canonical)));
    }

    private string ResolveLabel(string enumName)
    {
        string key = $"Enum:{typeof(TEnum).Name}.{enumName}";
        LocalizedString localized = _localizer[key];
        return localized.ResourceNotFound ? enumName : localized.Value;
    }
}
