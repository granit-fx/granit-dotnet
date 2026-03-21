using Granit.Diagnostics.Dtos;
using Granit.Diagnostics.Internal;
using Granit.Diagnostics.Options;
using Granit.Timing;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Diagnostics.Tests;

public sealed class HealthCheckAggregatorTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly HealthCheckService _healthCheckService = Substitute.For<HealthCheckService>();
    private readonly HealthCheckAggregator _sut;

    public HealthCheckAggregatorTests()
    {
        _clock.Now.Returns(_ => FixedNow);

        IOptions<DiagnosticsOptions> options = Microsoft.Extensions.Options.Options.Create(new DiagnosticsOptions
        {
            MonitoringCacheDuration = TimeSpan.FromSeconds(30),
        });

        _sut = new HealthCheckAggregator(_healthCheckService, _clock, options);
    }

    public void Dispose() => _sut.Dispose();

    [Fact]
    public async Task CheckAll_Maps_Healthy_To_Healthy()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 5.2));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services.ShouldHaveSingleItem();
        result.Services[0].Status.ShouldBe("healthy");
    }

    [Fact]
    public async Task CheckAll_Maps_Degraded_To_Degraded()
    {
        SetupReport(("redis", HealthStatus.Degraded, 150.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Status.ShouldBe("degraded");
    }

    [Fact]
    public async Task CheckAll_Maps_Unhealthy_To_Down()
    {
        SetupReport(("keycloak", HealthStatus.Unhealthy, 0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Status.ShouldBe("down");
    }

    [Fact]
    public async Task CheckAll_Returns_ResponseTimeMs_From_Duration()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 12.345));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].ResponseTimeMs.ShouldBe(12.3);
    }

    [Fact]
    public async Task CheckAll_Returns_CheckedAt_From_Clock()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 1.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.CheckedAt.ShouldBe(FixedNow);
    }

    [Fact]
    public async Task CheckAll_Returns_Tags_From_HealthCheck()
    {
        HealthReportEntry entry = new(
            HealthStatus.Healthy,
            description: null,
            duration: TimeSpan.FromMilliseconds(5),
            exception: null,
            data: null,
            tags: ["readiness", "startup"]);

        HealthReport report = new(
            new Dictionary<string, HealthReportEntry> { ["postgresql"] = entry },
            totalDuration: TimeSpan.FromMilliseconds(5));

        _healthCheckService.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(report);

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Tags.ShouldBe(["readiness", "startup"]);
    }

    [Fact]
    public async Task CheckAll_Formats_DisplayName_From_Registration()
    {
        SetupReport(("blob-storage-s3", HealthStatus.Healthy, 3.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Id.ShouldBe("blob-storage-s3");
        result.Services[0].Name.ShouldBe("Blob Storage S3");
    }

    [Fact]
    public async Task CheckAll_Caches_Result_Within_CacheDuration()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 5.0));

        await _sut.CheckAllAsync(TestContext.Current.CancellationToken);
        await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        await _healthCheckService.Received(1).CheckHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAll_Refreshes_After_CacheDuration_Expires()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 5.0));

        DateTimeOffset now = FixedNow;
        _clock.Now.Returns(_ => now);

        await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        // Advance past cache duration
        now = FixedNow.AddSeconds(31);
        _clock.Now.Returns(_ => now);

        await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        await _healthCheckService.Received(2).CheckHealthAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CheckAll_Returns_Multiple_Services()
    {
        SetupReport(
            ("postgresql", HealthStatus.Healthy, 5.0),
            ("keycloak", HealthStatus.Degraded, 200.0),
            ("redis", HealthStatus.Unhealthy, 0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services.Count.ShouldBe(3);
    }

    [Fact]
    public async Task CheckAll_Formats_SingleWord_DisplayName()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 1.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Name.ShouldBe("Postgresql");
    }

    [Fact]
    public async Task CheckAll_Formats_Underscore_DisplayName()
    {
        SetupReport(("blob_storage", HealthStatus.Healthy, 1.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Name.ShouldBe("Blob Storage");
    }

    [Fact]
    public async Task CheckAll_Passes_Description_Through()
    {
        HealthReportEntry entry = new(
            HealthStatus.Degraded,
            description: "Connection pool exhausted",
            duration: TimeSpan.FromMilliseconds(50),
            exception: null,
            data: null,
            tags: []);

        HealthReport report = new(
            new Dictionary<string, HealthReportEntry> { ["redis"] = entry },
            totalDuration: TimeSpan.FromMilliseconds(50));

        _healthCheckService.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(report);

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Description.ShouldBe("Connection pool exhausted");
    }

    [Fact]
    public async Task CheckAll_Returns_Null_Description_WhenNotProvided()
    {
        SetupReport(("postgresql", HealthStatus.Healthy, 1.0));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].Description.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAll_DoubleCheckLock_ReturnsCachedResult_WhenSecondCallerEntersAfterCachePopulated()
    {
        SemaphoreSlim firstCallerStarted = new(0, 1);
        SemaphoreSlim firstCallerCanContinue = new(0, 1);
        int callCount = 0;

        _healthCheckService.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                Interlocked.Increment(ref callCount);
                firstCallerStarted.Release();
                await firstCallerCanContinue.WaitAsync();

                return new HealthReport(
                    new Dictionary<string, HealthReportEntry>
                    {
                        ["test"] = new(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(1), null, null, [])
                    },
                    totalDuration: TimeSpan.FromMilliseconds(1));
            });

        Task<MonitoringHealthResponse> firstCall = _sut.CheckAllAsync(TestContext.Current.CancellationToken);
        await firstCallerStarted.WaitAsync(TestContext.Current.CancellationToken);

        Task<MonitoringHealthResponse> secondCall = _sut.CheckAllAsync(TestContext.Current.CancellationToken);
        firstCallerCanContinue.Release();

        MonitoringHealthResponse firstResult = await firstCall;
        MonitoringHealthResponse secondResult = await secondCall;

        callCount.ShouldBe(1);
        firstResult.ShouldBeSameAs(secondResult);
    }

    [Fact]
    public async Task CheckAll_Rounds_ResponseTimeMs_ToOneDecimal()
    {
        SetupReport(("db", HealthStatus.Healthy, 12.789));

        MonitoringHealthResponse result = await _sut.CheckAllAsync(TestContext.Current.CancellationToken);

        result.Services[0].ResponseTimeMs.ShouldBe(12.8);
    }

    [Fact]
    public void Dispose_DoesNotThrow_WhenCalledMultipleTimes()
    {
        IOptions<DiagnosticsOptions> options = Microsoft.Extensions.Options.Options.Create(new DiagnosticsOptions());
        HealthCheckAggregator aggregator = new(_healthCheckService, _clock, options);

        Should.NotThrow(aggregator.Dispose);
    }

    private void SetupReport(params (string Name, HealthStatus Status, double DurationMs)[] entries)
    {
        Dictionary<string, HealthReportEntry> dict = new(entries.Length);
        double totalMs = 0;

        foreach ((string name, HealthStatus status, double durationMs) in entries)
        {
            dict[name] = new HealthReportEntry(
                status,
                description: null,
                duration: TimeSpan.FromMilliseconds(durationMs),
                exception: null,
                data: null,
                tags: []);
            totalMs += durationMs;
        }

        HealthReport report = new(dict, totalDuration: TimeSpan.FromMilliseconds(totalMs));

        _healthCheckService.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(report);
    }
}
