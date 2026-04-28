using Granit.Domain;

namespace Granit.Dashboards.Domain.ValueObjects;

/// <summary>Strongly-typed identifier for a <see cref="WidgetInstance"/>.</summary>
public sealed class WidgetInstanceId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>Creates a new <see cref="WidgetInstanceId"/>.</summary>
    public static WidgetInstanceId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Widget instance identifier must not be empty.", nameof(value));
        }

        return new WidgetInstanceId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(WidgetInstanceId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator WidgetInstanceId(Guid value) => Create(value);
}
