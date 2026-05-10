using System.Diagnostics.Metrics;

namespace Granit.IO.Tests;

/// <summary>
/// Minimal <see cref="IMeterFactory"/> for tests: each call creates a new <see cref="Meter"/>
/// scoped to the factory so it can be disposed deterministically.
/// </summary>
internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        Meter meter = new(options);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose()
    {
        foreach (Meter meter in _meters)
        {
            meter.Dispose();
        }

        _meters.Clear();
    }
}
