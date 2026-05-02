using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Layouts;

/// <summary>
/// Fluent builder for a calendar layout declaration. Mirrors the kanban
/// builder shape: typed property selectors, opt-ins for
/// <see cref="EntityListLayoutDescriptor.IsDefault"/> and
/// <see cref="EntityListLayoutDescriptor.RequiresPermission"/>, and a
/// <c>Build()</c> step that surfaces missing-required-config as an
/// <see cref="InvalidOperationException"/> at host startup.
/// </summary>
/// <typeparam name="TEntity">The entity rendered on the time axis.</typeparam>
public sealed class CalendarLayoutBuilder<TEntity>
{
    private string? _startPropertyName;
    private string? _endPropertyName;
    private string? _titlePropertyName;
    private string? _colorByPropertyName;
    private bool _isDefault;
    private string? _requiresPermission;

    internal CalendarLayoutBuilder() { }

    /// <summary>
    /// Names the property carrying the event start. The lambda must be a direct
    /// property access (e.g. <c>i =&gt; i.StartsAt</c>); <c>DateTimeOffset</c>
    /// is the framework-mandated date type.
    /// </summary>
    public CalendarLayoutBuilder<TEntity> StartField(Expression<Func<TEntity, DateTimeOffset>> propertySelector)
    {
        _startPropertyName = ReadPropertyName(propertySelector, "StartField");
        return this;
    }

    /// <summary>
    /// Names the property carrying the event end. Optional — events without an
    /// end render as point-in-time markers. Nullable to support open-ended
    /// events (a meeting with no scheduled finish).
    /// </summary>
    public CalendarLayoutBuilder<TEntity> EndField(Expression<Func<TEntity, DateTimeOffset?>> propertySelector)
    {
        _endPropertyName = ReadPropertyName(propertySelector, "EndField");
        return this;
    }

    /// <summary>
    /// Names the property used as the event headline on the tile. Generic on
    /// <typeparamref name="TProperty"/>: the renderer stringifies the value
    /// (enum names, primitive <c>ToString()</c>) so non-string properties —
    /// statuses, ids, numerics — work as titles without forcing the host to
    /// duplicate a string projection on the entity.
    /// </summary>
    public CalendarLayoutBuilder<TEntity> TitleField<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
    {
        _titlePropertyName = ReadPropertyName(propertySelector, "TitleField");
        return this;
    }

    /// <summary>
    /// Names the property used to bucket events into colour groups (typically
    /// an enum or status). The renderer maps each distinct value to a colour
    /// from a stable palette.
    /// </summary>
    public CalendarLayoutBuilder<TEntity> ColorBy<TValue>(Expression<Func<TEntity, TValue>> propertySelector)
    {
        _colorByPropertyName = ReadPropertyName(propertySelector, "ColorBy");
        return this;
    }

    /// <summary>
    /// Marks this layout as the one the renderer picks on first load. At most
    /// one layout per entity may opt in.
    /// </summary>
    public CalendarLayoutBuilder<TEntity> IsDefault()
    {
        _isDefault = true;
        return this;
    }

    /// <summary>
    /// Drops the layout from the manifest payload entirely when the user lacks
    /// this permission — defense in depth, never just hidden.
    /// </summary>
    public CalendarLayoutBuilder<TEntity> RequiresPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _requiresPermission = permissionName;
        return this;
    }

    internal CalendarLayoutDescriptor Build()
    {
        if (_startPropertyName is null)
        {
            throw new InvalidOperationException(
                "CalendarView requires StartField(...) — declare which property carries the event start.");
        }

        return new CalendarLayoutDescriptor
        {
            Kind = EntityListLayoutKind.Calendar,
            IsDefault = _isDefault,
            RequiresPermission = _requiresPermission,
            StartPropertyName = _startPropertyName,
            EndPropertyName = _endPropertyName,
            TitlePropertyName = _titlePropertyName,
            ColorByPropertyName = _colorByPropertyName,
        };
    }

    private static string ReadPropertyName<TValue>(
        Expression<Func<TEntity, TValue>> selector,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(selector);

        Expression body = selector.Body is UnaryExpression { NodeType: ExpressionType.Convert } unary
            ? unary.Operand
            : selector.Body;

        if (body is not MemberExpression member || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                $"{parameterName} selector must be a direct property access expression (e.g. i => i.StartsAt).",
                nameof(selector));
        }

        return property.Name;
    }
}
