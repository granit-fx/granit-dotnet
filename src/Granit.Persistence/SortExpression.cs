using System.Linq.Expressions;

namespace Granit.Persistence;

/// <summary>
/// Describes a sort criterion with direction.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <param name="KeySelector">Expression selecting the property to sort by.</param>
/// <param name="Ascending"><c>true</c> for ascending, <c>false</c> for descending.</param>
public sealed record SortExpression<T>(
    Expression<Func<T, object>> KeySelector,
    bool Ascending);
