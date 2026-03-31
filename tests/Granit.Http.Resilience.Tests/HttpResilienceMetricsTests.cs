using System.Diagnostics.Metrics;
using Granit.Http.Resilience.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Http.Resilience.Tests;

public sealed class HttpResilienceMetricsTests : IDisposable
{
    private readonly TestMeterFactory _meterFactory = new();
    private readonly HttpResilienceMetrics _metrics;

    public HttpResilienceMetricsTests()
    {
        _metrics = new HttpResilienceMetrics(_meterFactory);
    }

    public void Dispose() => _meterFactory.Dispose();

    [Fact]
    public void MeterName_IsGranitHttpResilience() =>
        HttpResilienceMetrics.MeterName.ShouldBe("Granit.Http.Resilience");

    [Fact]
    public void RecordRetryTriggered_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordRetryTriggered("tenant-1", "my-client", 2));

    [Fact]
    public void RecordRetryTriggered_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordRetryTriggered(null, "my-client", 1));

    [Fact]
    public void RecordCircuitBreakerStateChanged_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordCircuitBreakerStateChanged("tenant-1", "my-client", "Open"));

    [Fact]
    public void RecordCircuitBreakerStateChanged_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordCircuitBreakerStateChanged(null, "my-client", "HalfOpen"));

    [Fact]
    public void RecordTimeoutOccurred_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTimeoutOccurred("tenant-1", "my-client"));

    [Fact]
    public void RecordTimeoutOccurred_WithNullTenant_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordTimeoutOccurred(null, "my-client"));

    [Fact]
    public void RecordRetryTriggered_EmitsCorrectMeasurement()
    {
        using MeterListener listener = new();
        long recordedValue = 0;
        string? recordedClientName = null;
        string? recordedTenantId = null;
        string? recordedAttempt = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HttpResilienceMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "granit.http.resilience.retry.triggered")
            {
                recordedValue = value;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    switch (tag.Key)
                    {
                        case "client_name":
                            recordedClientName = tag.Value?.ToString();
                            break;
                        case "tenant_id":
                            recordedTenantId = tag.Value?.ToString();
                            break;
                        case "attempt_number":
                            recordedAttempt = tag.Value?.ToString();
                            break;
                    }
                }
            }
        });
        listener.Start();

        _metrics.RecordRetryTriggered("t1", "test-api", 3);

        recordedValue.ShouldBe(1);
        recordedClientName.ShouldBe("test-api");
        recordedTenantId.ShouldBe("t1");
        recordedAttempt.ShouldBe("3");
    }

    [Fact]
    public void RecordCircuitBreakerStateChanged_EmitsCorrectMeasurement()
    {
        using MeterListener listener = new();
        long recordedValue = 0;
        string? recordedState = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HttpResilienceMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "granit.http.resilience.circuit_breaker.state_changed")
            {
                recordedValue = value;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "state")
                    {
                        recordedState = tag.Value?.ToString();
                    }
                }
            }
        });
        listener.Start();

        _metrics.RecordCircuitBreakerStateChanged("t1", "my-client", "Open");

        recordedValue.ShouldBe(1);
        recordedState.ShouldBe("Open");
    }

    [Fact]
    public void RecordTimeoutOccurred_EmitsCorrectMeasurement()
    {
        using MeterListener listener = new();
        long recordedValue = 0;
        string? recordedClient = null;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == HttpResilienceMetrics.MeterName)
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "granit.http.resilience.timeout.occurred")
            {
                recordedValue = value;
                foreach (KeyValuePair<string, object?> tag in tags)
                {
                    if (tag.Key == "client_name")
                    {
                        recordedClient = tag.Value?.ToString();
                    }
                }
            }
        });
        listener.Start();

        _metrics.RecordTimeoutOccurred(null, "timeout-client");

        recordedValue.ShouldBe(1);
        recordedClient.ShouldBe("timeout-client");
    }

    private sealed class TestMeterFactory : IMeterFactory
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
}
