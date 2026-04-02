using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Granit.Auditing.Diagnostics;
using Granit.Auditing.Domain;
using Granit.Auditing.Events;
using Granit.Auditing.Internal.Services;
using Granit.Auditing.Messages;
using Granit.Auditing.Options;
using Granit.Events;
using Granit.Guids;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Internal.Services;

public sealed class AuditingPersistenceWorkerTests : IDisposable
{
    private readonly Channel<AuditingBatch> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAuditBatchPersister _persister = Substitute.For<IAuditBatchPersister>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly AuditingMetrics _metrics;
    private readonly IMeterFactory _meterFactory;

    public AuditingPersistenceWorkerTests()
    {
        _channel = Channel.CreateUnbounded<AuditingBatch>();
        _meterFactory = new TestMeterFactory();
        _metrics = new AuditingMetrics(_meterFactory, _channel);

        IServiceScope scope = Substitute.For<IServiceScope>();
        IServiceProvider sp = Substitute.For<IServiceProvider>();

        IGuidGenerator guidGenerator = Substitute.For<IGuidGenerator>();
        guidGenerator.Create().Returns(_ => Guid.NewGuid());

        sp.GetService(typeof(IAuditBatchPersister)).Returns(_persister);
        sp.GetService(typeof(IDistributedEventBus)).Returns(_eventBus);
        sp.GetService(typeof(IGuidGenerator)).Returns(guidGenerator);
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

    private AuditingPersistenceWorker CreateWorker(AuditPersistenceMode mode = AuditPersistenceMode.Async)
    {
        AuditingOptions opts = new() { PersistenceMode = mode };
        IOptions<AuditingOptions> options = Microsoft.Extensions.Options.Options.Create(opts);

        return new AuditingPersistenceWorker(
            _channel,
            _scopeFactory,
            options,
            _metrics,
            NullLogger<AuditingPersistenceWorker>.Instance);
    }

    private static AuditingBatch CreateBatch(Guid? tenantId = null) => new(
        Timestamp: DateTimeOffset.UtcNow,
        UserId: "user-1",
        UserName: "Test User",
        Category: AuditCategory.DataMutation,
        IpAddress: null,
        UserAgent: null,
        TenantId: tenantId,
        CorrelationId: null,
        EntityChanges: [new AuditEntityChangeSnapshot("Order", "42", AuditChangeType.Created, [])]);

    private static async Task RunWorkerAsync(AuditingPersistenceWorker worker, int delayMs = 500)
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await worker.StartAsync(ct);
        await Task.Delay(delayMs, ct);
        await worker.StopAsync(ct);
    }

    [Fact]
    public async Task ExecuteAsync_DrainsChannelAndCallsPersister()
    {
        AuditingBatch batch1 = CreateBatch();
        AuditingBatch batch2 = CreateBatch();

        await _channel.Writer.WriteAsync(batch1, TestContext.Current.CancellationToken);
        await _channel.Writer.WriteAsync(batch2, TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        await RunWorkerAsync(worker);

        await _persister.Received(2).PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>());
        await _persister.Received(1).PersistAsync(batch1, Arg.Any<CancellationToken>());
        await _persister.Received(1).PersistAsync(batch2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnTransientFailure()
    {
        _persister.PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new InvalidOperationException("transient"),
                _ => Task.CompletedTask);

        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        // First retry delay is 200ms, need enough time for retry + persist.
        await RunWorkerAsync(worker, delayMs: 2_000);

        await _persister.Received(2).PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_DropsBatchAfterMaxRetries()
    {
        _persister.PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<InvalidOperationException>();

        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        // Retry delays sum to ~6.2s, allow 8s for all retries to complete.
        await RunWorkerAsync(worker, delayMs: 8_000);

        // 1 initial + 3 retries = 4 total calls
        await _persister.Received(4).PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_RecordsMetricsOnSuccess()
    {
        long persistedCount = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "granit.auditing.entry.persisted")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
            Interlocked.Add(ref persistedCount, measurement));
        listener.Start();

        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        await RunWorkerAsync(worker);

        listener.RecordObservableInstruments();
        persistedCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsCaptureErrorOnDrop()
    {
        long errorCount = 0;
        using MeterListener listener = new();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "granit.auditing.capture.errors")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) =>
            Interlocked.Add(ref errorCount, measurement));
        listener.Start();

        _persister.PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<InvalidOperationException>();

        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        await RunWorkerAsync(worker, delayMs: 8_000);

        listener.RecordObservableInstruments();
        errorCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesProcessingAfterDroppedBatch()
    {
        int callCount = 0;
        _persister.PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                int current = Interlocked.Increment(ref callCount);
                // Fail all attempts for first batch (4 calls: 1 initial + 3 retries)
                if (current <= 4)
                {
                    throw new InvalidOperationException("transient");
                }

                return Task.CompletedTask;
            });

        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        await RunWorkerAsync(worker, delayMs: 8_000);

        // 4 calls for first batch (all fail) + 1 call for second batch (succeeds)
        await _persister.Received(5).PersistAsync(Arg.Any<AuditingBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_PublishesIntegrationEventOnSuccess()
    {
        await _channel.Writer.WriteAsync(CreateBatch(), TestContext.Current.CancellationToken);
        _channel.Writer.Complete();

        AuditingPersistenceWorker worker = CreateWorker();
        await RunWorkerAsync(worker);

        await _eventBus.Received(1).PublishAsync(
            Arg.Any<AuditEntryPersistedEto>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Minimal <see cref="IMeterFactory"/> for testing metrics without DI.
    /// </summary>
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
