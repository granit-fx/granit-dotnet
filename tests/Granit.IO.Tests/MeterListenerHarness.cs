using System.Diagnostics.Metrics;

namespace Granit.IO.Tests;

/// <summary>
/// Lightweight harness for inspecting <c>long</c>-valued metric emissions on a given meter.
/// </summary>
internal sealed class MeterListenerHarness : IDisposable
{
    private readonly MeterListener _listener;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, long> _longSums = new(StringComparer.Ordinal);

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

        _listener.SetMeasurementEventCallback<long>((inst, value, _, _) =>
        {
            lock (_gate)
            {
                _longSums.TryGetValue(inst.Name, out long current);
                _longSums[inst.Name] = current + value;
            }
        });

        _listener.Start();
    }

    public long LongCounts(string instrumentName)
    {
        lock (_gate)
        {
            return _longSums.TryGetValue(instrumentName, out long v) ? v : 0;
        }
    }

    public void Dispose() => _listener.Dispose();
}
