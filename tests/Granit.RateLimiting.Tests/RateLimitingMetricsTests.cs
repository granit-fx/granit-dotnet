using System.Diagnostics.Metrics;
using Granit.RateLimiting.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class RateLimitingMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly RateLimitingMetrics _metrics;

    public RateLimitingMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        _meterFactory = sp.GetRequiredService<IMeterFactory>();
        _metrics = new RateLimitingMetrics(_meterFactory);
    }

    public void Dispose() =>
        (_meterFactory as IDisposable)?.Dispose();

    [Fact]
    public void MeterName_IsCorrect() =>
        RateLimitingMetrics.MeterName.ShouldBe("Granit.RateLimiting");

    [Fact]
    public void RecordAllowed_WithTenantId_DoesNotThrow() => Should.NotThrow(() => _metrics.RecordAllowed("api", "tenant-123"));

    [Fact]
    public void RecordAllowed_WithNullTenantId_DoesNotThrow() => Should.NotThrow(() => _metrics.RecordAllowed("api", null));

    [Fact]
    public void RecordRejected_WithTenantId_DoesNotThrow() => Should.NotThrow(() => _metrics.RecordRejected("api", "tenant-123"));

    [Fact]
    public void RecordRejected_WithNullTenantId_DoesNotThrow() => Should.NotThrow(() => _metrics.RecordRejected("api", null));

    [Fact]
    public void RecordAllowed_IncrementsCounter()
    {
        // Use a dedicated MeterFactory so previous test calls don't leak into the count.
        ServiceCollection services = new();
        services.AddMetrics();
        using ServiceProvider sp = services.BuildServiceProvider();
        IMeterFactory factory = sp.GetRequiredService<IMeterFactory>();
        RateLimitingMetrics metrics = new(factory);

        using MeterListener listener = new();
        long count = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.rate_limiting.requests.allowed")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            count += measurement;
        });

        listener.Start();

        metrics.RecordAllowed("api", "tenant-1");
        metrics.RecordAllowed("api", "tenant-1");

        listener.RecordObservableInstruments();

        count.ShouldBe(2);
    }

    [Fact]
    public void RecordRejected_IncrementsCounter()
    {
        // Use a dedicated MeterFactory so previous test calls don't leak into the count.
        ServiceCollection services = new();
        services.AddMetrics();
        using ServiceProvider sp = services.BuildServiceProvider();
        IMeterFactory factory = sp.GetRequiredService<IMeterFactory>();
        RateLimitingMetrics metrics = new(factory);

        using MeterListener listener = new();
        long count = 0;

        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Name == "granit.rate_limiting.requests.rejected")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            count += measurement;
        });

        listener.Start();

        metrics.RecordRejected("api", null);
        metrics.RecordRejected("api", null);
        metrics.RecordRejected("api", null);

        listener.RecordObservableInstruments();

        count.ShouldBe(3);
    }
}
