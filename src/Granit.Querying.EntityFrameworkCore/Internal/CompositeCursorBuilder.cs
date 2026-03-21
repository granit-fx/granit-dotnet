using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Builds compound WHERE expressions for composite keyset cursor pagination.
/// Supports multiple sort fields with mixed ascending/descending directions.
/// </summary>
/// <remarks>
/// For sort=<c>-createdAt,id</c> and cursor=<c>{createdAt: "2024-01-15", id: "abc"}</c>,
/// the generated expression is:
/// <code>
/// WHERE (createdAt &lt; @createdAt)
///    OR (createdAt = @createdAt AND id &gt; @id)
/// </code>
/// </remarks>
internal static class CompositeCursorBuilder
{
    /// <summary>
    /// Describes a sort field with its direction and resolved property info.
    /// </summary>
    internal readonly record struct SortField(PropertyInfo Property, bool Descending);

    /// <summary>
    /// Parses a sort specification string into a list of <see cref="SortField"/> entries.
    /// Only includes fields that exist as public instance properties on <typeparamref name="T"/>.
    /// </summary>
    public static List<SortField> ParseSortFields<T>(string sort)
    {
        List<SortField> fields = [];

        string[] parts = sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            bool descending = part.StartsWith('-');
            string fieldName = descending ? part[1..] : part;

            PropertyInfo? property = typeof(T).GetProperty(
                fieldName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is not null)
            {
                fields.Add(new SortField(property, descending));
            }
        }

        return fields;
    }

    /// <summary>
    /// Builds a compound WHERE expression from sort fields and cursor values.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="sortFields">The parsed sort fields with directions.</param>
    /// <param name="cursorValues">Dictionary mapping field names to their string cursor values.</param>
    /// <param name="logger">Optional logger for conversion failures.</param>
    /// <returns>A predicate expression, or <c>null</c> if the cursor cannot be built.</returns>
    public static Expression<Func<T, bool>>? BuildCursorPredicate<T>(
        IReadOnlyList<SortField> sortFields,
        Dictionary<string, string> cursorValues,
        ILogger? logger = null)
        where T : class
    {
        if (sortFields.Count == 0)
        {
            return null;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(T), "e");
        Expression? combined = null;

        // Build compound inequality: for N sort fields, we generate N OR branches.
        // Branch i: fields[0..i-1] are EQUAL, field[i] is GREATER/LESS (based on direction).
        for (int i = 0; i < sortFields.Count; i++)
        {
            Expression? branch = BuildBranch(parameter, sortFields, cursorValues, i, logger);
            if (branch is null)
            {
                continue;
            }

            combined = combined is null ? branch : Expression.OrElse(combined, branch);
        }

        if (combined is null)
        {
            return null;
        }

        return Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    /// <summary>
    /// Encodes cursor values from the last item for all sort fields.
    /// </summary>
    public static string EncodeCompositeCursor<T>(T lastItem, IReadOnlyList<SortField> sortFields)
        where T : class
    {
        var values = sortFields
            .Select(field => (field.Property.Name, Value: field.Property.GetValue(lastItem)))
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Name, x => x.Value!.ToString()!, StringComparer.OrdinalIgnoreCase);

        return CursorEncoder.EncodeComposite(values);
    }

    private static Expression? BuildBranch(
        ParameterExpression parameter,
        IReadOnlyList<SortField> sortFields,
        Dictionary<string, string> cursorValues,
        int targetIndex,
        ILogger? logger)
    {
        Expression? branch = null;

        // Equality conditions for fields before targetIndex
        for (int j = 0; j < targetIndex; j++)
        {
            SortField field = sortFields[j];
            if (!cursorValues.TryGetValue(field.Property.Name, out string? rawValue))
            {
                return null; // Missing cursor value — cannot build this branch
            }

            Type propertyType = Nullable.GetUnderlyingType(field.Property.PropertyType) ?? field.Property.PropertyType;
            object? converted = FilterExpressionBuilder.ConvertValue(rawValue, propertyType, logger, field.Property.Name);
            if (converted is null)
            {
                return null;
            }

            MemberExpression member = Expression.Property(parameter, field.Property);
            ConstantExpression constant = Expression.Constant(converted, field.Property.PropertyType);
            BinaryExpression equality = Expression.Equal(member, constant);

            branch = branch is null ? equality : Expression.AndAlso(branch, equality);
        }

        // Inequality condition for targetIndex field
        Expression? inequality = BuildInequalityCondition(parameter, sortFields[targetIndex], cursorValues, logger);
        if (inequality is null)
        {
            return null;
        }

        return branch is null ? inequality : Expression.AndAlso(branch, inequality);
    }

    private static BinaryExpression? BuildInequalityCondition(
        ParameterExpression parameter,
        SortField targetField,
        Dictionary<string, string> cursorValues,
        ILogger? logger)
    {
        if (!cursorValues.TryGetValue(targetField.Property.Name, out string? rawValue))
        {
            return null;
        }

        Type propertyType = Nullable.GetUnderlyingType(targetField.Property.PropertyType) ?? targetField.Property.PropertyType;
        object? converted = FilterExpressionBuilder.ConvertValue(rawValue, propertyType, logger, targetField.Property.Name);
        if (converted is null)
        {
            return null;
        }

        MemberExpression member = Expression.Property(parameter, targetField.Property);
        ConstantExpression constant = Expression.Constant(converted, targetField.Property.PropertyType);

        // Descending sort → cursor moves backward (less than), ascending → forward (greater than)
        return targetField.Descending
            ? Expression.LessThan(member, constant)
            : Expression.GreaterThan(member, constant);
    }
}
