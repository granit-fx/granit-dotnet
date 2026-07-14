using System.Diagnostics.Metrics;

namespace Granit.DataExchange.BackgroundJobs.Tests;

/// <summary>
/// Lightweight harness for inspecting <c>long</c>-valued metric emissions on a given meter,
/// keyed by instrument name + tag values. Mirrors the harness used by <c>Granit.IO.Tests</c>.
/// </summary>
internal sealed class MeterListenerHarness : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Lock _gate = new();
    private readonly List<(string Instrument, long Value, KeyValuePair<string, object?>[] Tags)> _measurements = [];

    public MeterListenerHarness(string meterName)
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == meterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };

        _listener.SetMeasurementEventCallback<long>((inst, value, tags, _) =>
        {
            lock (_gate)
            {
                _measurements.Add((inst.Name, value, tags.ToArray()));
            }
        });

        _listener.Start();
    }

    public long SumFor(string instrumentName, string tagKey, string tagValue)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => m.Instrument == instrumentName
                    && m.Tags.Any(t => t.Key == tagKey && Equals(t.Value, tagValue)))
                .Sum(m => m.Value);
        }
    }

    public void Dispose() => _listener.Dispose();
}
