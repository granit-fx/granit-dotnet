using System.Diagnostics.Metrics;

namespace Granit.AI.Tools.Tests.Fakes;

/// <summary>Minimal <see cref="IMeterFactory"/> for constructing metrics in tests.</summary>
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
