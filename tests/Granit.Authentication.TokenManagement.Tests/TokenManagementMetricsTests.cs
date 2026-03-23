using System.Diagnostics.Metrics;
using Granit.Authentication.TokenManagement.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Authentication.TokenManagement.Tests;

public sealed class TokenManagementMetricsTests : IDisposable
{
    private readonly TestMeterFactory _meterFactory = new();
    private readonly TokenManagementMetrics _sut;

    public TokenManagementMetricsTests()
    {
        _sut = new TokenManagementMetrics(_meterFactory);
    }

    [Fact]
    public void RecordTokenRequest_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordTokenRequest("tenant-1", "client_credentials", "my-client"));
    }

    [Fact]
    public void RecordTokenRequest_WithNullTenantId_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordTokenRequest(null, "authorization_code", "my-client"));
    }

    [Fact]
    public void RecordCacheHit_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordCacheHit("tenant-1", "my-client"));
    }

    [Fact]
    public void RecordCacheMiss_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordCacheMiss(null, "my-client"));
    }

    [Fact]
    public void RecordRevocation_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordRevocation("tenant-1"));
    }

    [Fact]
    public void RecordError_DoesNotThrow()
    {
        Should.NotThrow(() => _sut.RecordError(null, "invalid_grant"));
    }

    [Fact]
    public void MeterName_FollowsConvention()
    {
        TokenManagementMetrics.MeterName.ShouldBe("Granit.Authentication.TokenManagement");
    }

    [Fact]
    public void RecordTokenRequest_IncrementsCounter()
    {
        using var collector = new MeterListener();
        long count = 0;

        collector.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "granit.authentication.token.requests")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        collector.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            count += measurement;
        });

        collector.Start();

        _sut.RecordTokenRequest("tenant-1", "client_credentials", "my-client");

        collector.RecordObservableInstruments();

        count.ShouldBe(1);
    }

    public void Dispose() => _meterFactory.Dispose();

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
