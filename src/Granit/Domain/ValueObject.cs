namespace Granit.Domain;

/// <summary>
/// Base class for value objects — immutable types defined by their structural equality
/// rather than by an identifier.
/// </summary>
/// <remarks>
/// <para>
/// Subclasses must override <see cref="GetEqualityComponents"/> to return the properties
/// that define equality. All other equality members are handled automatically.
/// </para>
/// <para>
/// Value objects should be immutable: use <c>init</c> or <c>private set</c> for properties
/// and provide a constructor or factory method for creation.
/// </para>
/// <example>
/// <code>
/// public sealed class Money : ValueObject
/// {
///     public decimal Amount { get; init; }
///     public string Currency { get; init; } = string.Empty;
///
///     protected override IEnumerable&lt;object?&gt; GetEqualityComponents()
///     {
///         yield return Amount;
///         yield return Currency;
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public abstract class ValueObject : IEquatable<ValueObject>, IEqualityComparer<ValueObject>
{
    /// <summary>
    /// Returns the components used for equality comparison.
    /// Each component is compared using its default equality comparer.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    /// <inheritdoc />
    public bool Equals(ValueObject? other)
    {
        if (other is null || GetType() != other.GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    /// <inheritdoc />
    public sealed override bool Equals(object? obj) =>
        obj is ValueObject other && Equals(other);

    /// <inheritdoc />
    public sealed override int GetHashCode()
    {
        HashCode hash = default;
        foreach (object? component in GetEqualityComponents())
        {
            hash.Add(component);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    bool IEqualityComparer<ValueObject>.Equals(ValueObject? x, ValueObject? y) =>
        Equals(x, y);

    /// <inheritdoc />
    int IEqualityComparer<ValueObject>.GetHashCode(ValueObject obj) =>
        obj.GetHashCode();

    /// <summary>Equality operator.</summary>
    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        Equals(left, right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(ValueObject? left, ValueObject? right) =>
        !Equals(left, right);
}
