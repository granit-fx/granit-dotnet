using System.Linq.Expressions;
using System.Reflection;
using Granit.Domain;

namespace Granit.QueryEngine.EntityFrameworkCore.Internal;

/// <summary>
/// Resolves the member expression to use for a column. For a property marked
/// <see cref="QueryableValueObjectAttribute"/> whose type is a <see cref="SingleValueObject{T}"/>,
/// it drills into <c>.Value</c> — under the ComplexProperty mapping (ADR-070, strategy B) that is a
/// genuinely mapped scalar column, so substring/range/group-by translate. Otherwise it returns the
/// property member unchanged.
/// </summary>
internal static class ValueObjectMemberResolver
{
    public static Expression Resolve(Expression instance, PropertyInfo property)
    {
        MemberExpression member = Expression.Property(instance, property);
        return IsQueryableValueObject(property)
            ? Expression.Property(member, nameof(SingleValueObject<string>.Value))
            : member;
    }

    public static bool IsQueryableValueObject(PropertyInfo property) =>
        property.GetCustomAttribute<QueryableValueObjectAttribute>() is not null
        && GetSingleValueObjectBase(property.PropertyType) is not null;

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
}
