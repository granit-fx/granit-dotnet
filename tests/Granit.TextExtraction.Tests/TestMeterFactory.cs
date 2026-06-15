using System.Diagnostics.Metrics;

namespace Granit.TextExtraction.Tests;

internal sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    /// <summary>
    /// Meters created by this factory. A <see cref="MeterListener"/> filters by instrument
    /// name only, which is process-global — so a parallel test recording the same metric on
    /// another <c>TextExtractionMetrics</c> (same meter name) would leak into a listener that
    /// keys on name alone. Scope the listener to these instances to isolate per test.
    /// </summary>
    public IReadOnlyList<Meter> Meters => _meters;

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
