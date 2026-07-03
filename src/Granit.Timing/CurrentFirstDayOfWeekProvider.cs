namespace Granit.Timing;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-based implementation of <see cref="ICurrentFirstDayOfWeekProvider"/>.
/// Thread-safe and isolated per async execution context.
/// </summary>
public sealed class CurrentFirstDayOfWeekProvider : ICurrentFirstDayOfWeekProvider
{
    private readonly AsyncLocal<DayOfWeek?> _current = new();

    /// <inheritdoc />
    public DayOfWeek? FirstDayOfWeek
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
