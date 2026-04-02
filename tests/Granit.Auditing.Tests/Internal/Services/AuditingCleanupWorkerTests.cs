using System.Diagnostics.Metrics;
using System.Reflection;
using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Internal.Services;

public sealed class AuditingCleanupWorkerTests : IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditingCleaner _cleaner = Substitute.For<IAuditingCleaner>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly Channel<AuditingBatch> _channel;
    private readonly AuditingMetrics _metrics;
    private readonly AuditingOptions _options;
    private readonly IOptionsMonitor<AuditingOptions> _optionsMonitor;
    private readonly IMeterFactory _meterFactory;

    private readonly DateTimeOffset _fixedTime = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);

    public AuditingCleanupWorkerTests()
    {
        _channel = Channel.CreateUnbounded<AuditingBatch>();
        _meterFactory = new TestMeterFactory();
        _metrics = new AuditingMetrics(_meterFactory, _channel);
        _clock.Now.Returns(_fixedTime);

        _options = new AuditingOptions
        {
            CleanupBatchSize = 1000,
            CleanupInterval = TimeSpan.FromHours(24),
        };

        _optionsMonitor = Substitute.For<IOptionsMonitor<AuditingOptions>>();
        _optionsMonitor.CurrentValue.Returns(_options);

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider sp = Substitute.For<IServiceProvider>();

        sp.GetService(typeof(IAuditingCleaner)).Returns(_cleaner);
        scope.ServiceProvider.Returns(sp);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateAsyncScope().Returns(new AsyncServiceScope(scope));
    }

    public void Dispose()
    {
        if (_meterFactory is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private AuditingCleanupWorker CreateWorker() =>
        new(
            _scopeFactory,
            _optionsMonitor,
            _clock,
            _metrics,
            NullLogger<AuditingCleanupWorker>.Instance);

    /// <summary>
    /// Invokes the private <c>PurgeExpiredEntriesAsync</c> method via reflection
    /// to skip the initial delay and loop in <c>ExecuteAsync</c>.
    /// </summary>
    private static async Task InvokePurgeAsync(AuditingCleanupWorker worker, CancellationToken ct)
    {
        MethodInfo? purgeMethod = typeof(AuditingCleanupWorker).GetMethod(
            "PurgeExpiredEntriesAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        purgeMethod.ShouldNotBeNull();

        await (Task)purgeMethod.Invoke(worker, [ct])!;
    }

    [Fact]
    public async Task PurgeExpiredEntries_CallsCleanerForEachCategory()
    {
        _cleaner.PurgeAsync(Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        AuditingCleanupWorker worker = CreateWorker();
        await InvokePurgeAsync(worker, TestContext.Current.CancellationToken);

        AuditCategory[] categories = Enum.GetValues<AuditCategory>();

        foreach (AuditCategory category in categories)
        {
            await _cleaner.Received().PurgeAsync(
                category,
                Arg.Any<DateTimeOffset>(),
                _options.CleanupBatchSize,
                Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task PurgeExpiredEntries_UsesCorrectRetentionCutoff()
    {
        _options.DataMutationRetention = TimeSpan.FromDays(365);

        _cleaner.PurgeAsync(Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        AuditingCleanupWorker worker = CreateWorker();
        await InvokePurgeAsync(worker, TestContext.Current.CancellationToken);

        DateTimeOffset expectedCutoff = _fixedTime - TimeSpan.FromDays(365);

        await _cleaner.Received().PurgeAsync(
            AuditCategory.DataMutation,
            expectedCutoff,
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PurgeExpiredEntries_ContinuesBatchingUntilLessThanBatchSize()
    {
        _options.CleanupBatchSize = 100;

        // Simulate: DataMutation returns full batch (100) then partial (50).
        _cleaner.PurgeAsync(
                AuditCategory.DataMutation,
                Arg.Any<DateTimeOffset>(),
                100,
                Arg.Any<CancellationToken>())
            .Returns(100, 50);

        // Other categories return 0 immediately.
        _cleaner.PurgeAsync(
                Arg.Is<AuditCategory>(c => c != AuditCategory.DataMutation),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        AuditingCleanupWorker worker = CreateWorker();
        await InvokePurgeAsync(worker, TestContext.Current.CancellationToken);

        await _cleaner.Received(2).PurgeAsync(
            AuditCategory.DataMutation,
            Arg.Any<DateTimeOffset>(),
            100,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_LogsFailureOnException()
    {
        _cleaner.PurgeAsync(Arg.Any<AuditCategory>(), Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<InvalidOperationException>();

        AuditingCleanupWorker worker = CreateWorker();

        // PurgeExpiredEntriesAsync does NOT catch; ExecuteAsync does.
        // Calling PurgeExpiredEntriesAsync directly should throw.
        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await InvokePurgeAsync(worker, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PurgeExpiredEntries_RecordsPurgedMetrics()
    {
        long purgedCount = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "granit.auditing.entry.purged")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
            Interlocked.Add(ref purgedCount, measurement));
        listener.Start();

        // DataMutation returns 42 then 0.
        _cleaner.PurgeAsync(
                AuditCategory.DataMutation,
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(42, 0);

        // Other categories return 0.
        _cleaner.PurgeAsync(
                Arg.Is<AuditCategory>(c => c != AuditCategory.DataMutation),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(0);

        AuditingCleanupWorker worker = CreateWorker();
        await InvokePurgeAsync(worker, TestContext.Current.CancellationToken);

        listener.RecordObservableInstruments();
        purgedCount.ShouldBe(42);
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
