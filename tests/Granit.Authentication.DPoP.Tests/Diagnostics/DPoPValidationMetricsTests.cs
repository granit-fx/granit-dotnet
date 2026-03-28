using System.Diagnostics.Metrics;
using Granit.Authentication.DPoP.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Authentication.DPoP.Tests.Diagnostics;

public sealed class DPoPValidationMetricsTests : IDisposable
{
    private readonly ServiceProvider _sp;
    private readonly DPoPValidationMetrics _metrics;

    public DPoPValidationMetricsTests()
    {
        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        IMeterFactory meterFactory = _sp.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();
        _metrics = new DPoPValidationMetrics(meterFactory);
    }

    public void Dispose() => _sp.Dispose();

    [Fact]
    public void MeterName_IsCorrect() =>
        DPoPValidationMetrics.MeterName.ShouldBe("Granit.Authentication.DPoP");

    [Fact]
    public void RecordSuccess_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordSuccess("tenant-1"));

    [Fact]
    public void RecordSuccess_NullTenant_UsesGlobal() =>
        Should.NotThrow(() => _metrics.RecordSuccess(null));

    [Fact]
    public void RecordFailure_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordFailure("proof_expired", "tenant-1"));

    [Fact]
    public void RecordFailure_NullTenant_UsesGlobal() =>
        Should.NotThrow(() => _metrics.RecordFailure("invalid_algorithm", null));

    [Fact]
    public void RecordReplayDetected_DoesNotThrow() =>
        Should.NotThrow(() => _metrics.RecordReplayDetected("tenant-1"));

    [Fact]
    public void RecordReplayDetected_NullTenant_UsesGlobal() =>
        Should.NotThrow(() => _metrics.RecordReplayDetected(null));
}
