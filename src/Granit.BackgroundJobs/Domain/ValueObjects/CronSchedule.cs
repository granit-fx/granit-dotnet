using Granit.Core.Domain;

namespace Granit.BackgroundJobs.Domain.ValueObjects;

/// <summary>
/// A cron expression (5 or 6 fields) defining a recurrence schedule.
/// Maximum 100 characters.
/// </summary>
public sealed class CronSchedule : SingleValueObject<string>
{
    private const int MaxLength = 100;

    /// <inheritdoc />
    public override required string Value { get; init; }

    /// <summary>
    /// Creates a validated <see cref="CronSchedule"/>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When <paramref name="value"/> is empty, exceeds 100 characters, or does not have 5-6 fields.
    /// </exception>
    public static CronSchedule Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (value.Length > MaxLength)
        {
            throw new ArgumentException(
                $"Cron expression exceeds maximum length of {MaxLength} characters.", nameof(value));
        }

        int fieldCount = value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (fieldCount is not (5 or 6))
        {
            throw new ArgumentException(
                $"Cron expression must have 5 or 6 fields, got {fieldCount}.", nameof(value));
        }

        return new CronSchedule { Value = value };
    }

    /// <summary>Implicit conversion to <see cref="string"/>.</summary>
    public static implicit operator string(CronSchedule schedule) => schedule.Value;

    /// <summary>Implicit conversion from <see cref="string"/>.</summary>
    public static implicit operator CronSchedule(string value) => Create(value);
}
