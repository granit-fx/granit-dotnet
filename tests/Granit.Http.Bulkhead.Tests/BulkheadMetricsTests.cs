using System.Diagnostics.Metrics;
using Granit.Http.Bulkhead.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadMetricsTests : IDisposable
{
    private readonly IMeterFactory _meterFactory;
    private readonly BulkheadMetrics _sut;

    public BulkheadMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        _meterFactory = sp.GetRequiredService<IMeterFactory>();
        _sut = new BulkheadMetrics(_meterFactory);
    }

    public void Dispose() => (_meterFactory as IDisposable)?.Dispose();

    [Fact]
    public void MeterName_IsCorrect() =>
        BulkheadMetrics.MeterName.ShouldBe("Granit.Http.Bulkhead");

    [Fact]
    public void RecordAcquired_DoesNotThrow() => Should.NotThrow(() => _sut.RecordAcquired("api", "tenant-1"));

    [Fact]
    public void RecordAcquired_NullTenantId_DoesNotThrow() => Should.NotThrow(() => _sut.RecordAcquired("api", null));

    [Fact]
    public void RecordReleased_DoesNotThrow() => Should.NotThrow(() => _sut.RecordReleased("api", "tenant-1"));

    [Fact]
    public void RecordReleased_NullTenantId_DoesNotThrow() => Should.NotThrow(() => _sut.RecordReleased("api", null));

    [Fact]
    public void RecordRejected_DoesNotThrow() => Should.NotThrow(() => _sut.RecordRejected("api", "tenant-1"));

    [Fact]
    public void RecordRejected_NullTenantId_DoesNotThrow() => Should.NotThrow(() => _sut.RecordRejected("api", null));
}
