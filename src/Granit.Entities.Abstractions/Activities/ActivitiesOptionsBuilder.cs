using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Activities;

/// <summary>
/// Fluent builder for <see cref="ActivitiesDescriptor"/>. Used inside the
/// <c>EntityDefinitionBuilder&lt;T&gt;.Activities(b =&gt; ...)</c> opt-in
/// (ADR-046 §3).
/// </summary>
/// <typeparam name="TEntity">The entity type that hosts activities.</typeparam>
public sealed class ActivitiesOptionsBuilder<TEntity>
    where TEntity : class
{
    private readonly List<string> _allowedTypeNames = [];
    private string? _defaultAssigneePropertyName;

    /// <summary>
    /// Restricts the activity type catalog this entity offers to the listed
    /// names. Names that don't resolve in <see cref="IActivityRegistry"/> at
    /// manifest time are silently dropped (per ADR-045 §3) — call sites can
    /// safely list types contributed by optional modules.
    /// </summary>
    /// <param name="activityTypeNames">Activity type names (e.g. <c>"Call"</c>, <c>"Quote"</c>). Empty argument list is rejected — call <see cref="ActivitiesOptionsBuilder{TEntity}"/>'s parameterless constructor pathway (i.e. omit <c>AllowedTypes</c>) to allow every registered type.</param>
    public ActivitiesOptionsBuilder<TEntity> AllowedTypes(params string[] activityTypeNames)
    {
        ArgumentNullException.ThrowIfNull(activityTypeNames);
        if (activityTypeNames.Length == 0)
        {
            throw new ArgumentException(
                "AllowedTypes(...) must list at least one activity type name. Omit the call entirely to allow every registered type.",
                nameof(activityTypeNames));
        }
        foreach (string name in activityTypeNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            _allowedTypeNames.Add(name);
        }
        return this;
    }

    /// <summary>
    /// Names the host-entity property (PascalCase) that the React shell
    /// pre-fills as the activity assignee when creating an activity from this
    /// entity's detail page. Lambda must be a direct property access.
    /// </summary>
    public ActivitiesOptionsBuilder<TEntity> DefaultAssignee<TProperty>(Expression<Func<TEntity, TProperty>> propertySelector)
    {
        ArgumentNullException.ThrowIfNull(propertySelector);

        if (propertySelector.Body is not MemberExpression member
            || member.Member is not PropertyInfo property)
        {
            throw new ArgumentException(
                "DefaultAssignee selector must be a direct property access expression (e.g. x => x.AccountManagerUserId).",
                nameof(propertySelector));
        }

        _defaultAssigneePropertyName = property.Name;
        return this;
    }

    internal ActivitiesDescriptor Build() =>
        new(
            AllowedTypeNames: _allowedTypeNames.AsReadOnly(),
            DefaultAssigneePropertyName: _defaultAssigneePropertyName);
}
