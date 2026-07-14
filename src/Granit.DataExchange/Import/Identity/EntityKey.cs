namespace Granit.DataExchange.Import.Identity;

/// <summary>
/// A structural key identifying a row for identity resolution — one or more component values
/// (a single business key property, a composite key, or a primary key) compared by value.
/// </summary>
/// <remarks>
/// Two keys are equal when they carry the same number of components and every component is
/// equal (ordinal comparison for strings, <c>null</c>-aware). Used as a dictionary key when
/// batching identity resolution and prefetching existing rows, so equality and hashing must be
/// structural rather than reference-based.
/// </remarks>
public readonly struct EntityKey : IEquatable<EntityKey>
{
    private readonly object?[] _components;

    /// <summary>
    /// Creates a key from one or more component values, in a stable order
    /// (e.g. the order the business key properties were declared in).
    /// </summary>
    /// <param name="components">The key component values.</param>
    public EntityKey(params object?[] components)
    {
        ArgumentNullException.ThrowIfNull(components);
        _components = components;
    }

    /// <summary>The key's component values, in declaration order.</summary>
    public IReadOnlyList<object?> Components => _components ?? [];

    /// <inheritdoc/>
    public bool Equals(EntityKey other)
    {
        IReadOnlyList<object?> left = Components;
        IReadOnlyList<object?> right = other.Components;

        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!ComponentEquals(left[i], right[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EntityKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        HashCode hash = default;
        foreach (object? component in Components)
        {
            hash.Add(component is string s ? s.GetHashCode(StringComparison.Ordinal) : component);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc/>
    public override string ToString() =>
        $"[{string.Join(", ", Components.Select(static c => c?.ToString() ?? "null"))}]";

    public static bool operator ==(EntityKey left, EntityKey right) => left.Equals(right);

    public static bool operator !=(EntityKey left, EntityKey right) => !left.Equals(right);

    private static bool ComponentEquals(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        return left is string leftString && right is string rightString
            ? string.Equals(leftString, rightString, StringComparison.Ordinal)
            : left.Equals(right);
    }
}
