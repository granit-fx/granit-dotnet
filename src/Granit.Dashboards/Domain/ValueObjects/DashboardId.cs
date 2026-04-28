using Granit.Domain;

namespace Granit.Dashboards.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="Dashboard"/>.</summary>
public sealed class DashboardId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="DashboardId"/>.</summary>
    public static DashboardId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Dashboard identifier must not be empty.", nameof(value));
        }

        return new DashboardId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(DashboardId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator DashboardId(Guid value) => Create(value);
}
