using System.Linq.Expressions;

namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about a single filter preset within a <see cref="FilterGroupDescriptor"/>.
/// </summary>
public sealed class PresetDescriptor
{
    /// <summary>Unique name of this preset within its group (e.g. <c>"Active"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>User-facing label, or <c>null</c> to use the name.</summary>
    public string? Label { get; init; }

    /// <summary>Whether this preset is active by default.</summary>
    public bool IsDefault { get; init; }

    /// <summary>The predicate expression to apply when this preset is active.</summary>
    public required LambdaExpression Predicate { get; init; }
}
