using System.Diagnostics.Metrics;
using Granit.Auditing.BackgroundJobs.Internal;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Auditing.BackgroundJobs.Tests.Internal;

public sealed class AuditRetentionCleanupServiceTests : IDisposable
{
    private static readonly int CategoryCount = Enum.GetValues<AuditCategory>().Length;

    private readonly IAuditingCleaner _cleaner = Substitute.For<IAuditingCleaner>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AuditingMetrics _metrics;
    private readonly IMeterFactory _meterFactory = new TestMeterFactory();

    public AuditRetentionCleanupServiceTests()
    {
        _metrics = new AuditingMetrics(_meterFactory);
        _clock.Now.Returns(DateTimeOffset.UnixEpoch.AddYears(50));

        IServiceProvider sp = Substitute.For<IServiceProvider>();
        sp.GetService(typeof(IAuditingCleaner)).Returns(_cleaner);
        IServiceScope scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(sp);
        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);
    }

    public void Dispose() => (_meterFactory as IDisposable)?.Dispose();

    private AuditRetentionCleanupService CreateService(int batchSize = 10_000)
    {
        AuditingOptions opts = new() { CleanupBatchSize = batchSize };
        return new AuditRetentionCleanupService(
            _scopeFactory, MsOptions.Create(opts), _clock, _metrics,
            NullLogger<AuditRetentionCleanupService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_PurgesEveryCategoryOnce_WhenNothingToDelete()
    {
        _cleaner.PurgeAsync(Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        await CreateService().ExecuteAsync(TestContext.Current.CancellationToken);

        await _cleaner.Received(CategoryCount).PurgeAsync(
            Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        foreach (AuditCategory category in Enum.GetValues<AuditCategory>())
        {
            await _cleaner.Received(1).PurgeAsync(
                category, Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task ExecuteAsync_KeepsPurging_WhileBatchIsFull()
    {
        // A full batch (== batchSize) means more rows remain: the loop must run again.
        // Returns 2, 2, 0 then 0 thereafter → first category loops 3×, the rest once each.
        _cleaner.PurgeAsync(Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(2, 2, 0);

        await CreateService(batchSize: 2).ExecuteAsync(TestContext.Current.CancellationToken);

        await _cleaner.Received(CategoryCount + 2).PurgeAsync(
            Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
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
        }
    }
}
