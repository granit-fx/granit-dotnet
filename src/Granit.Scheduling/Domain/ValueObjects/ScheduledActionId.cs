using Granit.Domain;

namespace Granit.Scheduling.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a <see cref="ScheduledAction"/>.
/// </summary>
public sealed class ScheduledActionId : SingleValueObject<Guid>
{
    /// <inheritdoc />
    public override required Guid Value { get; init; }

    /// <summary>
    /// Creates a new <see cref="ScheduledActionId"/> from the specified GUID.
    /// </summary>
    /// <param name="value">The GUID value.</param>
    /// <returns>A new <see cref="ScheduledActionId"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the GUID is empty.</exception>
    public static ScheduledActionId Create(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Scheduled action identifier must not be empty.", nameof(value));
        }

        return new ScheduledActionId { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="Guid"/>.</summary>
    public static implicit operator Guid(ScheduledActionId id) => id.Value;

    /// <summary>Implicit conversion from <see cref="Guid"/>.</summary>
    public static implicit operator ScheduledActionId(Guid value) => Create(value);
}
