using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

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
    /// Describes a sort field with its direction, resolved leaf property, and the (possibly dotted)
    /// canonical member path. <see cref="Path"/> equals the property name for a top-level field and a
    /// dotted path such as <c>"Value.Street1"</c> for a nested complex-type member.
    /// </summary>
    internal readonly record struct SortField(string Path, PropertyInfo Property, bool Descending);

    /// <summary>
    /// Parses a sort specification string into a list of <see cref="SortField"/> entries.
    /// Only includes fields that exist as public instance properties on <typeparamref name="T"/>
    /// AND are present in the <paramref name="allowedSortFields"/> whitelist.
    /// </summary>
    /// <param name="sort">Comma-separated sort specification (e.g. <c>"-createdAt,id"</c>).</param>
    /// <param name="allowedSortFields">
    /// Whitelist of allowed sort field names. When <c>null</c>, all public properties are accepted
    /// (backward compatibility for legacy callers).
    /// </param>
    public static List<SortField> ParseSortFields<T>(string sort, IReadOnlySet<string>? allowedSortFields = null)
    {
        List<SortField> fields = [];

        string[] parts = sort.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (string part in parts)
        {
            bool descending = part.StartsWith('-');
            string fieldName = descending ? part[1..] : part;

            if (allowedSortFields?.Contains(fieldName) == false)
            {
                continue;
            }

            // Walk a (possibly dotted) path so a nested complex-member sort field such as
            // "Value.Street1" participates in the keyset — otherwise it would be silently dropped and
            // the cursor predicate would disagree with the ORDER BY, skipping/duplicating rows at
            // page boundaries. Canonicalises segment casing off the resolved PropertyInfo.
            (string CanonicalPath, PropertyInfo Leaf)? resolved = ResolvePath(typeof(T), fieldName);
            if (resolved is not null)
            {
                fields.Add(new SortField(resolved.Value.CanonicalPath, resolved.Value.Leaf, descending));
            }
        }

        return fields;
    }

    // Resolves a (possibly dotted) member path to its leaf PropertyInfo plus the canonical dotted path
    // (segment casing taken from the resolved properties). Returns null if any segment is unknown.
    private static (string CanonicalPath, PropertyInfo Leaf)? ResolvePath(Type root, string path)
    {
        Type current = root;
        PropertyInfo? property = null;
        List<string> canonical = [];

        foreach (string segment in path.Split('.'))
        {
            property = current.GetProperty(
                segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                return null;
            }

            canonical.Add(property.Name);
            current = property.PropertyType;
        }

        return property is null ? null : (string.Join('.', canonical), property);
    }

    // Builds a plain member-access chain for a (possibly dotted) path — no [QueryableValueObject]
    // drilling, so a top-level VO cursor key keeps comparing whole-value as it did before.
    private static Expression BuildMemberAccess(ParameterExpression parameter, string path)
    {
        Expression member = parameter;
        foreach (string segment in path.Split('.'))
        {
            member = Expression.Property(member, segment);
        }

        return member;
    }

    // Reads a (possibly dotted) member path off a materialized instance for cursor encoding.
    private static object? ReadPath(object instance, string path)
    {
        object? current = instance;
        foreach (string segment in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            PropertyInfo? property = current.GetType().GetProperty(
                segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
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
    public static string EncodeCompositeCursor<T>(
        T lastItem, IReadOnlyList<SortField> sortFields, byte[]? hmacKey = null)
        where T : class
    {
        var values = sortFields
            .Select(field => (field.Path, Value: ReadPath(lastItem, field.Path)))
            .Where(x => x.Value is not null)
            .ToDictionary(x => x.Path, x => x.Value!.ToString()!, StringComparer.OrdinalIgnoreCase);

        return CursorEncoder.EncodeComposite(values, hmacKey);
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
            if (!cursorValues.TryGetValue(field.Path, out string? rawValue))
            {
                return null; // Missing cursor value — cannot build this branch
            }

            Type propertyType = Nullable.GetUnderlyingType(field.Property.PropertyType) ?? field.Property.PropertyType;
            object? converted = FilterExpressionBuilder.ConvertValue(rawValue, propertyType, logger, field.Path);
            if (converted is null)
            {
                return null;
            }

            Expression member = BuildMemberAccess(parameter, field.Path);
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
        if (!cursorValues.TryGetValue(targetField.Path, out string? rawValue))
        {
            return null;
        }

        Type propertyType = Nullable.GetUnderlyingType(targetField.Property.PropertyType) ?? targetField.Property.PropertyType;
        object? converted = FilterExpressionBuilder.ConvertValue(rawValue, propertyType, logger, targetField.Path);
        if (converted is null)
        {
            return null;
        }

        Expression member = BuildMemberAccess(parameter, targetField.Path);
        ConstantExpression constant = Expression.Constant(converted, targetField.Property.PropertyType);

        // Descending sort → cursor moves backward (less than), ascending → forward (greater than).
        return BuildOrderComparison(member, constant, targetField.Descending);
    }

    // Builds the keyset "member {<,>} value" comparison. Numeric / temporal / enum columns expose the
    // relational operators directly. string, Guid and other operator-less IComparable scalars (e.g. the
    // default Guid `Id` tiebreaker, or any string sort field) have no `>`/`<` operator — building one
    // throws at expression-construction time — so they compare through `CompareTo`, which EF Core maps
    // to a relational SQL comparison.
    private static BinaryExpression BuildOrderComparison(Expression member, Expression constant, bool descending)
    {
        Type type = Nullable.GetUnderlyingType(member.Type) ?? member.Type;

        if (!HasRelationalOperator(type))
        {
            MethodInfo? compareTo = member.Type.GetMethod(nameof(IComparable.CompareTo), [member.Type]);
            if (compareTo is not null)
            {
                MethodCallExpression comparison = Expression.Call(member, compareTo, constant);
                ConstantExpression zero = Expression.Constant(0);
                return descending
                    ? Expression.LessThan(comparison, zero)
                    : Expression.GreaterThan(comparison, zero);
            }
        }

        return descending
            ? Expression.LessThan(member, constant)
            : Expression.GreaterThan(member, constant);
    }

    // Whether the type carries the built-in relational operators (so Expression.GreaterThan/LessThan is
    // valid). Excludes bool (no ordering) and reference/struct types like string and Guid that order
    // only through CompareTo.
    private static bool HasRelationalOperator(Type type) =>
        (type.IsPrimitive && type != typeof(bool))
        || type.IsEnum
        || type == typeof(decimal)
        || type == typeof(DateTime)
        || type == typeof(DateTimeOffset)
        || type == typeof(DateOnly)
        || type == typeof(TimeOnly)
        || type == typeof(TimeSpan);
}
