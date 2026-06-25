using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Granit.DataLookup.Descriptors;
using Granit.Entities.Visibility;

namespace Granit.Entities.Forms;

/// <summary>
/// Fluent builder for one form field. Carries the typed property reference at
/// declaration time; the resulting <see cref="FieldDescriptor"/> stores only the
/// property name + CLR type for runtime use.
/// </summary>
/// <typeparam name="TEntity">The owning entity type.</typeparam>
/// <typeparam name="TProperty">The property's CLR type.</typeparam>
public sealed class FieldBuilder<TEntity, TProperty>
{
    private readonly string _propertyName;
    private readonly Type _clrType;
    private readonly int _order;

    private string _component;
    private Dictionary<string, object?>? _config;
    private string? _labelKey;
    private string? _helpKey;
    private string? _requiresPermission;
    private bool _readOnly;
    private VisibilityCondition? _visibleIf;
    private LookupDescriptor? _lookup;

    internal FieldBuilder(Expression<Func<TEntity, TProperty>> propertySelector, int order)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "Field selector must be a direct property access expression (e.g. x => x.Title).",
                nameof(propertySelector));
        }

        _propertyName = property.Name;
        _clrType = typeof(TProperty);
        _component = ChooseDefaultComponent(typeof(TProperty));
        _order = order;
    }

    /// <summary>
    /// Sets the field component (per ADR-041): a name from the standard catalog
    /// (<c>"text"</c>, <c>"money"</c>, …) or <c>"custom:&lt;app-prefix&gt;-&lt;name&gt;"</c>
    /// for app-specific components. Optional config payload carried opaquely to the renderer.
    /// </summary>
    /// <remarks>
    /// "Component" replaces the previous "Widget" naming — dashboard panels keep "Widget"
    /// (KPI / Chart / Table / …) because they are large, page-level units; field-level
    /// renderers are small UI controls bound to a single property and "Component" matches
    /// the React mental model the front consumes.
    /// </remarks>
    public FieldBuilder<TEntity, TProperty> Component(string component, IReadOnlyDictionary<string, object?>? config = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);
        _component = component;
        _config = config is null ? null : new Dictionary<string, object?>(config, StringComparer.Ordinal);
        return this;
    }

    /// <summary>i18n key for the user-facing label (resolved client-side).</summary>
    public FieldBuilder<TEntity, TProperty> Label(string labelKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(labelKey);
        _labelKey = labelKey;
        return this;
    }

    /// <summary>i18n key for the help text shown under the field.</summary>
    public FieldBuilder<TEntity, TProperty> Help(string helpKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helpKey);
        _helpKey = helpKey;
        return this;
    }

    /// <summary>
    /// Drops the field from the manifest payload entirely when the user lacks this
    /// permission. Defense-in-depth — never just hidden, always absent.
    /// </summary>
    public FieldBuilder<TEntity, TProperty> RequiresPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _requiresPermission = permissionName;
        return this;
    }

    /// <summary>Marks the field as read-only in the form context.</summary>
    public FieldBuilder<TEntity, TProperty> ReadOnly()
    {
        _readOnly = true;
        return this;
    }

    /// <summary>
    /// Conditional visibility rule against another field (closed enum operators per
    /// ADR-040). Evaluated client-side; permission gating
    /// (<see cref="RequiresPermission"/>) is server-side and takes precedence.
    /// </summary>
    public FieldBuilder<TEntity, TProperty> VisibleIf(string otherField, FieldOp op, object? value = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(otherField);
        _visibleIf = new VisibilityCondition(otherField, op, value);
        return this;
    }

    /// <summary>
    /// Binds this field to a data-lookup source (typeahead picker) by registry name. The form
    /// renders a server-backed picker instead of a free-text or plain <c>select</c> control.
    /// Mirrors <c>ColumnBuilder.Lookup</c> on the query side, so a foreign-key field
    /// (<c>tenantId</c>, <c>ownerId</c>…) resolves the same source in both the grid filter and
    /// the edit form.
    /// </summary>
    /// <param name="name">Lookup registry key (e.g. <c>"tenants"</c>).</param>
    /// <param name="kind">The lookup kind. Defaults to <see cref="LookupKind.QueryEngine"/>.</param>
    /// <param name="requiredPermission">Optional permission the caller must hold.</param>
    /// <param name="scopeKeys">Scope keys the picker must supply (cascading pickers).</param>
    public FieldBuilder<TEntity, TProperty> Lookup(
        string name,
        LookupKind kind = LookupKind.QueryEngine,
        string? requiredPermission = null,
        IReadOnlyList<string>? scopeKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _lookup = new LookupDescriptor(
            Name: name,
            Kind: kind,
            RequiredPermission: requiredPermission,
            ScopeKeys: scopeKeys);
        return this;
    }

    /// <summary>
    /// Binds this field to a data-lookup source via a full <see cref="LookupDescriptor"/>. Use
    /// when pointing to a custom URL or overriding the default search parameter.
    /// </summary>
    public FieldBuilder<TEntity, TProperty> Lookup(LookupDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        _lookup = descriptor;
        return this;
    }

    internal FieldDescriptor Build() =>
        new()
        {
            PropertyName = _propertyName,
            ClrType = _clrType,
            Component = _component,
            Config = ResolveConfig(),
            LabelKey = _labelKey,
            HelpKey = _helpKey,
            RequiresPermission = _requiresPermission,
            Order = _order,
            ReadOnly = _readOnly,
            VisibleIf = _visibleIf,
            Lookup = _lookup,
        };

    /// <summary>
    /// Auto-populates <c>config["options"]</c> for an enum-backed choice component so the
    /// renderer never receives a <c>select</c>/<c>multiselect</c> with no options (ADR-041 —
    /// both require an <c>options[]</c> config). Skipped when the field declares a data lookup
    /// (the picker supplies its own values) or the caller already provided <c>options</c>.
    /// </summary>
    private Dictionary<string, object?>? ResolveConfig()
    {
        Type unwrapped = Nullable.GetUnderlyingType(_clrType) ?? _clrType;
        bool autoOptions = unwrapped.IsEnum
            && _lookup is null
            && _component is "select" or "multiselect" or "status"
            && (_config is null || !_config.ContainsKey("options"));

        if (!autoOptions)
        {
            return _config;
        }

        Dictionary<string, object?> config = _config is null
            ? new Dictionary<string, object?>(StringComparer.Ordinal)
            : new Dictionary<string, object?>(_config, StringComparer.Ordinal);
        config["options"] = BuildEnumOptions(unwrapped);
        return config;
    }

    /// <summary>
    /// Materializes an enum's members as <see cref="FieldSelectOption"/>s — value = the member's
    /// PascalCase name (symmetric with the wire enum format), label = the
    /// <c>Enum:{TypeName}.{Value}</c> i18n key (ADR-023, shared with <c>EnumLookupSource</c>).
    /// For <c>[Flags]</c> enums the zero member (the empty set) is dropped — selecting nothing
    /// already represents it in a multiselect.
    /// </summary>
    private static List<FieldSelectOption> BuildEnumOptions(Type enumType)
    {
        bool isFlags = enumType.IsDefined(typeof(FlagsAttribute), inherit: false);
        string[] names = Enum.GetNames(enumType);
        List<FieldSelectOption> options = new(names.Length);

        foreach (string name in names)
        {
            if (isFlags
                && Convert.ToInt64(Enum.Parse(enumType, name), CultureInfo.InvariantCulture) == 0)
            {
                continue;
            }

            options.Add(new FieldSelectOption(name, $"Enum:{enumType.Name}.{name}"));
        }

        return options;
    }

    /// <summary>
    /// Picks a sensible default component from the standard catalog (per ADR-041) based on the
    /// property's CLR type. Apps override via <see cref="Component(string, IReadOnlyDictionary{string, object?}?)"/>.
    /// </summary>
    private static string ChooseDefaultComponent(Type clrType)
    {
        Type unwrapped = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (unwrapped == typeof(bool))
        {
            return "boolean";
        }

        if (unwrapped == typeof(int) || unwrapped == typeof(long) || unwrapped == typeof(short) || unwrapped == typeof(byte))
        {
            return "integer";
        }

        if (unwrapped == typeof(decimal) || unwrapped == typeof(double) || unwrapped == typeof(float))
        {
            return "decimal";
        }

        if (unwrapped == typeof(DateOnly))
        {
            return "date";
        }

        if (unwrapped == typeof(TimeOnly))
        {
            return "time";
        }

        if (unwrapped == typeof(DateTime) || unwrapped == typeof(DateTimeOffset))
        {
            return "datetime";
        }

        if (unwrapped.IsEnum)
        {
            // [Flags] enums are a set, not a single choice → multiselect (ADR-041).
            return unwrapped.IsDefined(typeof(FlagsAttribute), inherit: false) ? "multiselect" : "select";
        }

        return "text";
    }
}
