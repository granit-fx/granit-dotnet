using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Granit.Domain;
using Granit.QueryEngine.EntityFrameworkCore.Diagnostics;
using Granit.QueryEngine.Filtering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

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
    /// <param name="shadowColumns">
    /// Optional dictionary of shadow property columns (name → descriptor) from the
    /// <see cref="QueryDefinitionBuilder{TEntity}"/>. When a field is not found via
    /// CLR reflection, this dictionary is checked for EF Core Shadow Properties.
    /// </param>
    /// <returns>A predicate expression, or <c>null</c> if the property is not found.</returns>
    public static Expression<Func<TEntity, bool>>? Build<TEntity>(
        FilterCriteria criteria,
        ILogger? logger = null,
        IReadOnlyDictionary<string, ColumnDescriptor>? shadowColumns = null)
        where TEntity : class
    {
        PropertyInfo? property = typeof(TEntity).GetProperty(
            criteria.Field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        Expression member;
        Type propertyType;

        if (property is not null)
        {
            // A [QueryableValueObject] column resolves to its `.Value` string column (ADR-070,
            // strategy B) so equality/IN/substring operate on the real scalar. Range operators are
            // dropped (a string value object has no meaningful ordering — the same stance as a
            // converter-mapped VO). A plain value-object column stays the VO type (equality
            // reconstructs it).
            if (ValueObjectMemberResolver.IsQueryableValueObject(property)
                && criteria.Operator is FilterOperator.Gt or FilterOperator.Gte
                    or FilterOperator.Lt or FilterOperator.Lte or FilterOperator.Between)
            {
                return null;
            }

            member = ValueObjectMemberResolver.Resolve(parameter, property);
            propertyType = Nullable.GetUnderlyingType(member.Type) ?? member.Type;
        }
        else if (shadowColumns is not null
                 && shadowColumns.TryGetValue(criteria.Field, out ColumnDescriptor? shadowCol))
        {
            // EF.Property<T>(entity, "Name") — translates to SQL column access
            member = BuildEfPropertyAccess(parameter, criteria.Field, shadowCol.ClrType);
            propertyType = Nullable.GetUnderlyingType(shadowCol.ClrType) ?? shadowCol.ClrType;
        }
        else
        {
            if (logger is not null)
            {
                QueryEngineEfCoreLog.FilterFieldNotFound(logger, criteria.Field, typeof(TEntity).Name);
            }

            return null;
        }

        Expression? body = criteria.Operator switch
        {
            FilterOperator.Eq => BuildEqualsExpression(member, criteria.Value, propertyType, logger, criteria.Field),
            FilterOperator.Contains => BuildStringMethodExpression(member, criteria.Value, StringContainsMethod, logger, criteria.Field),
            FilterOperator.StartsWith => BuildStringMethodExpression(member, criteria.Value, StringStartsWithMethod, logger, criteria.Field),
            FilterOperator.EndsWith => BuildStringMethodExpression(member, criteria.Value, StringEndsWithMethod, logger, criteria.Field),
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
        Expression member, string value, Type propertyType,
        ILogger? logger = null, string? field = null)
    {
        object? converted = ConvertValue(value, propertyType, logger, field);
        if (converted is null)
        {
            // Conversion failed (unparseable value, or a value object the engine cannot build):
            // drop the criterion. Previously, a null on a reference-typed column (e.g. a value
            // object) fell through and built `column == null`, silently matching the wrong rows.
            // See issue #2767.
            return null;
        }

        ConstantExpression constant = Expression.Constant(converted, member.Type);
        return Expression.Equal(member, constant);
    }

    private static BinaryExpression? BuildStringMethodExpression(
        Expression member, string value, MethodInfo method,
        ILogger? logger = null, string? field = null)
    {
        if (member.Type != typeof(string))
        {
            // A value-object column (SingleValueObject<string>) is mapped by a ValueConverter;
            // EF Core cannot translate LIKE over it, so a substring filter would either be
            // dropped or mistranslated. Surface it instead of silently returning no predicate
            // (which previously made the criterion vanish from the query). See issue #2767.
            if (logger is not null)
            {
                QueryEngineEfCoreLog.SubstringFilterOnNonStringColumnIgnored(
                    logger, field ?? "(unknown)", member.Type.Name);
            }

            return null;
        }

        // Escape LIKE wildcards to prevent wildcard injection (CWE-943)
        string sanitized = LikeWildcardEscaper.Escape(value);

        // Guard against null: (e.Property != null && e.Property.Contains(sanitizedValue))
        Expression notNull = Expression.NotEqual(member, Expression.Constant(null, typeof(string)));
        Expression call = Expression.Call(member, method, Expression.Constant(sanitized));
        return Expression.AndAlso(notNull, call);
    }

    private static BinaryExpression? BuildComparisonExpression(
        Expression member, string value, Type propertyType,
        Func<Expression, Expression, BinaryExpression> comparison,
        ILogger? logger = null, string? field = null)
    {
        // Value objects define no ordering operators — a Gt/Lt over one would throw at
        // expression build. Range operators are not meaningful on an identifier-like VO. See #2767.
        if (GetSingleValueObjectBase(member.Type) is not null)
        {
            return null;
        }

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
        Expression member, string value, Type propertyType,
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
        Expression member, string value, Type propertyType,
        ILogger? logger = null, string? field = null)
    {
        // See BuildComparisonExpression — range operators are unsupported on value objects.
        if (GetSingleValueObjectBase(member.Type) is not null)
        {
            return null;
        }

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

    /// <summary>
    /// Builds an <c>EF.Property&lt;T&gt;(entity, name)</c> expression for a Shadow Property.
    /// </summary>
    private static MethodCallExpression BuildEfPropertyAccess(
        ParameterExpression parameter, string propertyName, Type clrType)
    {
        // EF.Property<T>(entity, "PropertyName")
        MethodInfo efPropertyMethod = typeof(EF)
            .GetMethod(nameof(EF.Property))!
            .MakeGenericMethod(clrType);

        return Expression.Call(efPropertyMethod, parameter, Expression.Constant(propertyName));
    }

    internal static object? ConvertValue(
        string value, Type targetType, ILogger? logger = null, string? field = null)
    {
        try
        {
            // SingleValueObject<TPrimitive>: parse the string to the underlying primitive, then
            // reconstruct the value object so EF Core compares it through its ValueConverter
            // (whole-value equality / IN translate; substring/LIKE does not and is rejected on
            // the substring paths). See issue #2767.
            Type? svoBase = GetSingleValueObjectBase(targetType);
            if (svoBase is not null)
            {
                object? primitive = ConvertValue(value, svoBase.GetGenericArguments()[0], logger, field);
                return primitive is null ? null : ReconstructValueObject(targetType, primitive);
            }

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
                object parsed = Enum.Parse(targetType, value, ignoreCase: true);
                return Enum.IsDefined(targetType, parsed) ? parsed : null;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            LogConversionFailure(logger, field, targetType, ex);
            return null;
        }
    }

    private static void LogConversionFailure(ILogger? logger, string? field, Type targetType, Exception ex)
    {
        if (logger is not null)
        {
            QueryEngineEfCoreLog.FilterValueConversionFailed(logger, field ?? "(unknown)", targetType.Name, ex);
        }
    }

    // Returns the SingleValueObject<TPrimitive> base type for a value-object CLR type, or null.
    private static Type? GetSingleValueObjectBase(Type type)
    {
        Type openType = typeof(SingleValueObject<>);
        for (Type? current = type; current is not null && current != typeof(object); current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == openType)
            {
                return current;
            }
        }

        return null;
    }

    // Builds a value object whose Value equals the parsed primitive, without running the
    // domain factory's validation. The instance only feeds an EF Core comparison constant —
    // an invalid filter value simply matches no rows — so this mirrors how
    // SingleValueObjectConverter materialises rows from the database.
    private static object ReconstructValueObject(Type valueObjectType, object primitive)
    {
        object instance = RuntimeHelpers.GetUninitializedObject(valueObjectType);
        PropertyInfo valueProperty = valueObjectType.GetProperty(nameof(SingleValueObject<string>.Value))!;
        valueProperty.SetValue(instance, primitive);
        return instance;
    }
}
