namespace Granit.Timing;

/// <summary>
/// <see cref="AsyncLocal{T}"/>-based implementation of <see cref="ICurrentTimezoneProvider"/>.
/// Thread-safe and isolated per async execution context.
/// </summary>
public sealed class CurrentTimezoneProvider : ICurrentTimezoneProvider
{
    private readonly AsyncLocal<string?> _current = new();

    /// <inheritdoc />
    public string? Timezone
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}
