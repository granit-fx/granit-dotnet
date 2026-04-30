using System.Linq.Expressions;
using System.Reflection;

namespace Granit.Entities.Relations;

/// <summary>
/// Fluent builder for the aggregates a relation surfaces.
/// </summary>
/// <typeparam name="TRelated">The related entity type.</typeparam>
public sealed class RelationAggregateBuilder<TRelated> where TRelated : class
{
    private readonly List<RelationAggregateDescriptor> _aggregates = [];

    /// <summary>Adds a count of related rows.</summary>
    public RelationAggregateBuilder<TRelated> Count(string? labelKey = null, string? format = null)
    {
        _aggregates.Add(new RelationAggregateDescriptor(
            RelationAggregateKind.Count, PropertyName: null, labelKey, format));
        return this;
    }

    /// <summary>Adds a sum over a numeric property.</summary>
    public RelationAggregateBuilder<TRelated> Sum<TProperty>(
        Expression<Func<TRelated, TProperty>> property, string? labelKey = null, string? format = null) =>
        Add(RelationAggregateKind.Sum, property, labelKey, format);

    /// <summary>Adds an average over a numeric property.</summary>
    public RelationAggregateBuilder<TRelated> Avg<TProperty>(
        Expression<Func<TRelated, TProperty>> property, string? labelKey = null, string? format = null) =>
        Add(RelationAggregateKind.Avg, property, labelKey, format);

    /// <summary>Adds a min over a numeric property.</summary>
    public RelationAggregateBuilder<TRelated> Min<TProperty>(
        Expression<Func<TRelated, TProperty>> property, string? labelKey = null, string? format = null) =>
        Add(RelationAggregateKind.Min, property, labelKey, format);

    /// <summary>Adds a max over a numeric property.</summary>
    public RelationAggregateBuilder<TRelated> Max<TProperty>(
        Expression<Func<TRelated, TProperty>> property, string? labelKey = null, string? format = null) =>
        Add(RelationAggregateKind.Max, property, labelKey, format);

    private RelationAggregateBuilder<TRelated> Add<TProperty>(
        RelationAggregateKind kind,
        Expression<Func<TRelated, TProperty>> property,
        string? labelKey,
        string? format)
    {
        ArgumentNullException.ThrowIfNull(property);

        if (property.Body is not MemberExpression member
            || member.Member is not PropertyInfo info)
        {
            throw new ArgumentException(
                "Aggregate selector must be a direct property access expression (e.g. x => x.Amount).",
                nameof(property));
        }

        _aggregates.Add(new RelationAggregateDescriptor(kind, info.Name, labelKey, format));
        return this;
    }

    internal IReadOnlyList<RelationAggregateDescriptor> Build() => _aggregates;
}
