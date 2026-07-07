using System.Linq.Expressions;

namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about an independent toggleable filter.
/// Quick filters are individually activatable and combine with AND semantics.
/// Unlike <see cref="FilterGroupDescriptor"/> presets which are mutually exclusive (OR within a group),
/// quick filters act like independent checkboxes.
/// </summary>
public sealed record QuickFilterDescriptor
{
    /// <summary>Unique name of this filter (e.g. <c>"MyAppointments"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>User-facing label (e.g. <c>"Mes rendez-vous"</c>).</summary>
    public string? Label { get; init; }

    /// <summary>Whether this filter is active by default.</summary>
    public bool IsDefault { get; init; }

    /// <summary>The predicate expression to apply when this filter is active.</summary>
    public required LambdaExpression Predicate { get; init; }
}
