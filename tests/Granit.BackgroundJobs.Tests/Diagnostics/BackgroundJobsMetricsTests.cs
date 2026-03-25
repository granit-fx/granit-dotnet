using System.Diagnostics.Metrics;
using Granit.BackgroundJobs.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Diagnostics;

public sealed class BackgroundJobsMetricsTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMeterFactory _meterFactory;
    private readonly BackgroundJobsMetrics _sut;

    public BackgroundJobsMetricsTests()
    {
        _serviceProvider = new ServiceCollection()
            .AddMetrics()
            .BuildServiceProvider();
        _meterFactory = _serviceProvider.GetRequiredService<IMeterFactory>();
        _sut = new BackgroundJobsMetrics(_meterFactory);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public void MeterName_is_Granit_BackgroundJobs() =>
        BackgroundJobsMetrics.MeterName.ShouldBe("Granit.BackgroundJobs");

    [Fact]
    public void RecordExecutionCompleted_increments_counter_with_tags()
    {
        // Arrange
        using var collector = new MetricCollector<long>(
            _meterFactory, BackgroundJobsMetrics.MeterName, "granit.background_jobs.execution.completed");

        // Act
        _sut.RecordExecutionCompleted("tenant-abc", "daily-report", "success");

        // Assert
        CollectedMeasurement<long> measurement = collector.LastMeasurement!;
        measurement.Value.ShouldBe(1);
        measurement.ContainsTags(
            new KeyValuePair<string, object?>("tenant_id", "tenant-abc"),
            new KeyValuePair<string, object?>("job_name", "daily-report"),
            new KeyValuePair<string, object?>("status", "success")).ShouldBeTrue();
    }

    [Fact]
    public void RecordExecutionCompleted_null_tenantId_coalesces_to_global()
    {
        // Arrange
        using var collector = new MetricCollector<long>(
            _meterFactory, BackgroundJobsMetrics.MeterName, "granit.background_jobs.execution.completed");

        // Act
        _sut.RecordExecutionCompleted(null, "cleanup-job", "failure");

        // Assert
        CollectedMeasurement<long> measurement = collector.LastMeasurement!;
        measurement.ContainsTags(
            new KeyValuePair<string, object?>("tenant_id", "global"),
            new KeyValuePair<string, object?>("status", "failure")).ShouldBeTrue();
    }

    [Fact]
    public void RecordExecutionDuration_records_histogram_with_tags()
    {
        // Arrange
        using var collector = new MetricCollector<double>(
            _meterFactory, BackgroundJobsMetrics.MeterName, "granit.background_jobs.execution.duration");
        var duration = TimeSpan.FromSeconds(2.5);

        // Act
        _sut.RecordExecutionDuration("tenant-xyz", "sync-job", "success", duration);

        // Assert
        CollectedMeasurement<double> measurement = collector.LastMeasurement!;
        measurement.Value.ShouldBe(2.5);
        measurement.ContainsTags(
            new KeyValuePair<string, object?>("tenant_id", "tenant-xyz"),
            new KeyValuePair<string, object?>("job_name", "sync-job"),
            new KeyValuePair<string, object?>("status", "success")).ShouldBeTrue();
    }

    [Fact]
    public void RecordExecutionDuration_null_tenantId_coalesces_to_global()
    {
        // Arrange
        using var collector = new MetricCollector<double>(
            _meterFactory, BackgroundJobsMetrics.MeterName, "granit.background_jobs.execution.duration");
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        _sut.RecordExecutionDuration(null, "nightly-job", "success", duration);

        // Assert
        CollectedMeasurement<double> measurement = collector.LastMeasurement!;
        measurement.Value.ShouldBe(0.15);
        measurement.ContainsTags(
            new KeyValuePair<string, object?>("tenant_id", "global")).ShouldBeTrue();
    }
}
