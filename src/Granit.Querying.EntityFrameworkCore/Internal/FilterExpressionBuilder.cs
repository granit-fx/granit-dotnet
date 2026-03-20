using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Granit.Querying.EntityFrameworkCore.Diagnostics;
using Granit.Querying.Filtering;
using Microsoft.Extensions.Logging;

namespace Granit.Querying.EntityFrameworkCore.Internal;

/// <summary>
/// Builds <see cref="Expression{TDelegate}"/> predicates from <see cref="FilterCriteria"/>.
/// All operators are translated to expression trees — no dynamic LINQ or string interpolation.
/// </summary>
internal static class FilterExpressionBuilder
{
    private static readonly MethodInfo StringContainsMethod =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    private static readonly MethodInfo StringStartsWithMethod =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;

    private static readonly MethodInfo StringEndsWithMethod =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;

    /// <summary>
    /// Builds a predicate expression for a single filter criterion.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="criteria">The filter criterion.</param>
    /// <param name="logger">Optional logger for diagnosing conversion failures.</param>
    /// <returns>A predicate expression, or <c>null</c> if the property is not found.</returns>
    public static Expression<Func<TEntity, bool>>? Build<TEntity>(
        FilterCriteria criteria, ILogger? logger = null)
        where TEntity : class
    {
        PropertyInfo? property = typeof(TEntity).GetProperty(
            criteria.Field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null)
        {
            if (logger is not null)
            {
                QueryingEfCoreLog.FilterFieldNotFound(logger, criteria.Field, typeof(TEntity).Name);
            }

            return null;
        }

        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression member = Expression.Property(parameter, property);
        Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        Expression? body = criteria.Operator switch
        {
            FilterOperator.Eq => BuildEqualsExpression(member, criteria.Value, propertyType, logger, criteria.Field),
            FilterOperator.Contains => BuildStringMethodExpression(member, criteria.Value, StringContainsMethod),
            FilterOperator.StartsWith => BuildStringMethodExpression(member, criteria.Value, StringStartsWithMethod),
            FilterOperator.EndsWith => BuildStringMethodExpression(member, criteria.Value, StringEndsWithMethod),
            FilterOperator.Gt => BuildComparisonExpression(member, criteria.Value, propertyType, Expression.GreaterThan, logger, criteria.Field),
            FilterOperator.Gte => BuildComparisonExpression(member, criteria.Value, propertyType, Expression.GreaterThanOrEqual, logger, criteria.Field),
            FilterOperator.Lt => BuildComparisonExpression(member, criteria.Value, propertyType, Expression.LessThan, logger, criteria.Field),
            FilterOperator.Lte => BuildComparisonExpression(member, criteria.Value, propertyType, Expression.LessThanOrEqual, logger, criteria.Field),
            FilterOperator.In => BuildInExpression(member, criteria.Value, propertyType, logger, criteria.Field),
            FilterOperator.Between => BuildBetweenExpression(member, criteria.Value, propertyType, logger, criteria.Field),
            _ => null,
        };

        if (body is null)
        {
            return null;
        }

        return Expression.Lambda<Func<TEntity, bool>>(body, parameter);
    }

    private static BinaryExpression? BuildEqualsExpression(
        MemberExpression member, string value, Type propertyType,
        ILogger? logger = null, string? field = null)
    {
        object? converted = ConvertValue(value, propertyType, logger, field);
        if (converted is null && propertyType.IsValueType)
        {
            return null;
        }

        ConstantExpression constant = Expression.Constant(converted, member.Type);
        return Expression.Equal(member, constant);
    }

    private static BinaryExpression? BuildStringMethodExpression(
        MemberExpression member, string value, MethodInfo method)
    {
        if (member.Type != typeof(string))
        {
            return null;
        }

        // Guard against null: (e.Property != null && e.Property.Contains(value))
        Expression notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
        Expression call = Expression.Call(member, method, Expression.Constant(value));
        return Expression.AndAlso(notNull, call);
    }

    private static BinaryExpression? BuildComparisonExpression(
        MemberExpression member, string value, Type propertyType,
        Func<Expression, Expression, BinaryExpression> comparison,
        ILogger? logger = null, string? field = null)
    {
        object? converted = ConvertValue(value, propertyType, logger, field);
        if (converted is null)
        {
            return null;
        }

        Expression left = member;
        Expression right = Expression.Constant(converted, member.Type);

        // For nullable types, compare .Value
        if (Nullable.GetUnderlyingType(member.Type) is not null)
        {
            left = Expression.Property(member, "Value");
            right = Expression.Constant(converted, propertyType);
            Expression hasValue = Expression.Property(member, "HasValue");
            return Expression.AndAlso(hasValue, comparison(left, right));
        }

        return comparison(left, right);
    }

    private static Expression? BuildInExpression(
        MemberExpression member, string value, Type propertyType,
        ILogger? logger = null, string? field = null)
    {
        string[] parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<object> values = [];

        foreach (string part in parts)
        {
            object? converted = ConvertValue(part, propertyType, logger, field);
            if (converted is not null)
            {
                values.Add(converted);
            }
        }

        if (values.Count == 0)
        {
            return null;
        }

        // Build: new[] { v1, v2, v3 }.Contains(e.Property)
        Type listType = typeof(List<>).MakeGenericType(propertyType);
        object list = Activator.CreateInstance(listType)!;
        MethodInfo addMethod = listType.GetMethod("Add")!;
        foreach (object v in values)
        {
            addMethod.Invoke(list, [v]);
        }

        MethodInfo containsMethod = listType.GetMethod("Contains")!;
        Expression listExpression = Expression.Constant(list);

        Expression propertyExpression = member;
        if (Nullable.GetUnderlyingType(member.Type) is not null)
        {
            propertyExpression = Expression.Property(member, "Value");
            Expression hasValue = Expression.Property(member, "HasValue");
            return Expression.AndAlso(hasValue, Expression.Call(listExpression, containsMethod, propertyExpression));
        }

        return Expression.Call(listExpression, containsMethod, propertyExpression);
    }

    private static BinaryExpression? BuildBetweenExpression(
        MemberExpression member, string value, Type propertyType,
        ILogger? logger = null, string? field = null)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        object? lower = ConvertValue(parts[0], propertyType, logger, field);
        object? upper = ConvertValue(parts[1], propertyType, logger, field);
        if (lower is null || upper is null)
        {
            return null;
        }

        Expression left = member;
        Expression lowerExpr = Expression.Constant(lower, member.Type);
        Expression upperExpr = Expression.Constant(upper, member.Type);

        if (Nullable.GetUnderlyingType(member.Type) is not null)
        {
            left = Expression.Property(member, "Value");
            lowerExpr = Expression.Constant(lower, propertyType);
            upperExpr = Expression.Constant(upper, propertyType);
            Expression hasValue = Expression.Property(member, "HasValue");
            return Expression.AndAlso(
                hasValue,
                Expression.AndAlso(
                    Expression.GreaterThanOrEqual(left, lowerExpr),
                    Expression.LessThanOrEqual(left, upperExpr)));
        }

        return Expression.AndAlso(
            Expression.GreaterThanOrEqual(left, lowerExpr),
            Expression.LessThanOrEqual(left, upperExpr));
    }

    internal static object? ConvertValue(
        string value, Type targetType, ILogger? logger = null, string? field = null)
    {
        try
        {
            if (targetType == typeof(string))
            {
                return value;
            }

            if (targetType == typeof(Guid))
            {
                return Guid.Parse(value);
            }

            if (targetType == typeof(bool))
            {
                return bool.Parse(value);
            }

            if (targetType == typeof(DateTimeOffset))
            {
                return DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(DateTime))
            {
                return DateTime.Parse(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(DateOnly))
            {
                return DateOnly.Parse(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(TimeOnly))
            {
                return TimeOnly.Parse(value, CultureInfo.InvariantCulture);
            }

            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, value, ignoreCase: true);
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                QueryingEfCoreLog.FilterValueConversionFailed(
                    logger, field ?? "(unknown)", value, targetType.Name, ex);
            }

            return null;
        }
    }
}
