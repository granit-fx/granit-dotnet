using System.Linq.Expressions;
using System.Reflection;
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

    private string _widget;
    private Dictionary<string, object?>? _config;
    private string? _labelKey;
    private string? _helpKey;
    private string? _requiresPermission;
    private int _order;
    private bool _readOnly;
    private VisibilityCondition? _visibleIf;

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
        _widget = ChooseDefaultWidget(typeof(TProperty));
        _order = order;
    }

    /// <summary>
    /// Sets the widget (per ADR-041): a name from the standard catalog
    /// (<c>"text"</c>, <c>"money"</c>, …) or <c>"custom:&lt;app-prefix&gt;-&lt;name&gt;"</c>
    /// for app-specific widgets. Optional config payload carried opaquely to the renderer.
    /// </summary>
    public FieldBuilder<TEntity, TProperty> Widget(string widget, IReadOnlyDictionary<string, object?>? config = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(widget);
        _widget = widget;
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

    internal FieldDescriptor Build() =>
        new()
        {
            PropertyName = _propertyName,
            ClrType = _clrType,
            Widget = _widget,
            Config = _config,
            LabelKey = _labelKey,
            HelpKey = _helpKey,
            RequiresPermission = _requiresPermission,
            Order = _order,
            ReadOnly = _readOnly,
            VisibleIf = _visibleIf,
        };

    /// <summary>
    /// Picks a sensible default widget from the standard catalog (per ADR-041) based on the
    /// property's CLR type. Apps override via <see cref="Widget(string, IReadOnlyDictionary{string, object?}?)"/>.
    /// </summary>
    private static string ChooseDefaultWidget(Type clrType)
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
            return "select";
        }

        return "text";
    }
}
