using System.Reflection;
using Granit.QueryEngine;

namespace Granit.Analytics.EntityFrameworkCore.Internal;

/// <summary>
/// Whitelist-aware property resolver shared by Map / Chart / Pivot /
/// QueryAggregate runners. Validates that a caller-supplied field name is
/// declared on the <see cref="QueryDefinition{TEntity}"/> (the same column
/// whitelist the admin grid honours) BEFORE reflecting on the entity. Without
/// this gate, dashboard authors could aggregate, group-by, or popup-render
/// arbitrary public properties of the target entity — including columns the
/// QueryDefinition deliberately keeps off the grid (audit notes, internal
/// flags, per-row scores).
/// </summary>
internal static class AnalyticsColumnWhitelist
{
    /// <summary>
    /// Resolves <paramref name="fieldName"/> to its declared
    /// <see cref="ColumnDescriptor"/> on <paramref name="definition"/>, then
    /// to the matching <see cref="PropertyInfo"/> on
    /// <typeparamref name="TEntity"/>. Throws <see cref="ArgumentException"/>
    /// when the field is not declared, or when the declared column has no
    /// public instance property (e.g. shadow columns — those are not
    /// reachable from runtime reflection).
    /// </summary>
    public static (PropertyInfo Property, ColumnDescriptor Column) ResolveProperty<TEntity>(
        QueryDefinition<TEntity> definition,
        string queryName,
        string fieldName,
        string paramName)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldName);

        ColumnDescriptor? declared = null;
        foreach (ColumnDescriptor candidate in definition.GetColumns())
        {
            if (string.Equals(candidate.PropertyName, fieldName, StringComparison.OrdinalIgnoreCase))
            {
                declared = candidate;
                break;
            }
        }

        if (declared is null)
        {
            throw new ArgumentException(
                $"Field '{fieldName}' is not declared by query '{queryName}'.",
                paramName);
        }

        if (declared.IsShadowProperty)
        {
            throw new ArgumentException(
                $"Field '{declared.PropertyName}' on query '{queryName}' is a shadow property and is not supported by analytics runners.",
                paramName);
        }

        // Resolve via the declared (canonical) property name — case-insensitive
        // declared lookup tolerates frontend casing variations without exposing
        // undeclared columns.
        PropertyInfo? prop = typeof(TEntity).GetProperty(
            declared.PropertyName,
            BindingFlags.Public | BindingFlags.Instance);

        if (prop is null)
        {
            throw new ArgumentException(
                $"Declared column '{declared.PropertyName}' on query '{queryName}' has no public instance property.",
                paramName);
        }

        return (prop, declared);
    }
}
