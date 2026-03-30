// =============================================================================
// Tests — MigrationBatchWorker
// =============================================================================
// Verifies the BackgroundService cascade logic, graceful shutdown handling,
// batch timeout, and error propagation without requiring a real database.
// =============================================================================

using System.Threading.Channels;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationBatchWorkerTests : IDisposable
{
    private readonly Channel<RunMigrationBatchCommand> _channel = Channel.CreateUnbounded<RunMigrationBatchCommand>();
    private readonly MigrationProgressDbContext _progressContext;
    private readonly IClock _clock;

    public MigrationBatchWorkerTests()
    {
        DbContextOptions<MigrationProgressDbContext> options =
            new DbContextOptionsBuilder<MigrationProgressDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        _progressContext = new MigrationProgressDbContext(options);
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTimeOffset.UtcNow);
    }

    public void Dispose() => _progressContext.Dispose();

    private MigrationBatchWorker BuildWorker(
        IMigrationCycleRegistry registry,
        TimeSpan? batchTimeout = null)
    {
        MigrationStartupOptions opts = new();
        if (batchTimeout.HasValue)
        {
            opts.BatchExecutionTimeout = batchTimeout.Value;
        }

        StubDbContext stubDbContext = new(
            new DbContextOptionsBuilder<StubDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        ServiceCollection scopeServices = new();
        scopeServices.AddSingleton(registry);
        scopeServices.AddSingleton(stubDbContext);
        scopeServices.AddSingleton(_progressContext);
        scopeServices.AddSingleton(Substitute.For<ITenantDbIsolator>());
        scopeServices.AddSingleton(_clock);
        scopeServices.AddSingleton<IGuidGenerator>(new SimpleGuidGenerator());
        scopeServices.AddSingleton<ILogger<MigrationBatchExecutor>>(
            NullLogger<MigrationBatchExecutor>.Instance);
        scopeServices.AddScoped<MigrationBatchExecutor>();
        ServiceProvider scopeProvider = scopeServices.BuildServiceProvider();

        IServiceScopeFactory scopeFactory = scopeProvider.GetRequiredService<IServiceScopeFactory>();

        return new MigrationBatchWorker(
            _channel,
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<MigrationBatchWorker>.Instance);
    }

    private static IMigrationCycleRegistry RegistryWith(
        string cycleId, BatchMigrationDelegate migration)
    {
        IMigrationCycleRegistry registry = Substitute.For<IMigrationCycleRegistry>();
        registry.Find(cycleId).Returns(new MigrationCycleRegistration(
            cycleId, typeof(StubDbContext), migration));
        return registry;
    }

    // -------------------------------------------------------------------------
    // Basic cascade: processes command and stops
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_SingleBatchNoNextCursor_ProcessesAndStops()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "single-batch";
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, (_, _, _) => Task.FromResult(new MigrationBatchResult(10, null)));
        MigrationBatchWorker worker = BuildWorker(registry);

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);
        _channel.Writer.Complete();

        // Act
        await worker.StartAsync(cancellationToken);
        await worker.ExecuteTask!;

        // Assert
        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, cancellationToken);
        progress.ShouldNotBeNull();
        progress!.Status.ShouldBe(MigrationStatus.Completed);
        progress.ProcessedRows.ShouldBe(10);
    }

    // -------------------------------------------------------------------------
    // Cascade: two batches via next cursor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_CascadeTwoBatches_ProcessesBothAndCompletes()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "cascade";
        int callCount = 0;
        IMigrationCycleRegistry registry = RegistryWith(cycleId, (_, _, _) =>
        {
            callCount++;
            string? next = callCount == 1 ? "cursor-2" : null;
            return Task.FromResult(new MigrationBatchResult(50, next));
        });
        MigrationBatchWorker worker = BuildWorker(registry);

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);
        _channel.Writer.Complete();

        // Act
        await worker.StartAsync(cancellationToken);
        await worker.ExecuteTask!;

        // Assert
        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, cancellationToken);
        progress.ShouldNotBeNull();
        progress!.Status.ShouldBe(MigrationStatus.Completed);
        progress.ProcessedRows.ShouldBe(100);
        callCount.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Graceful shutdown: batch exception during cancellation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShutdownDuringBatch_StopsCascade()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "shutdown-during";
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        IMigrationCycleRegistry registry = RegistryWith(cycleId, async (_, _, _) =>
        {
            await workerCts.CancelAsync();
            throw new OperationCanceledException(workerCts.Token);
        });
        MigrationBatchWorker worker = BuildWorker(registry);

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);

        // Act
        await worker.StartAsync(workerCts.Token);
        await Task.Delay(500, cancellationToken);
        await worker.StopAsync(cancellationToken);

        // Assert
        worker.ExecuteTask!.IsCompleted.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Graceful shutdown between batches
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_ShutdownBetweenBatches_StopsBeforeNextCascade()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "shutdown-between";
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        int callCount = 0;

        IMigrationCycleRegistry registry = RegistryWith(cycleId, (_, _, _) =>
        {
            callCount++;
            if (callCount == 1)
            {
                workerCts.Cancel();
                return Task.FromResult(new MigrationBatchResult(50, "cursor-2"));
            }

            return Task.FromResult(new MigrationBatchResult(50, null));
        });
        MigrationBatchWorker worker = BuildWorker(registry);

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);

        // Act
        await worker.StartAsync(workerCts.Token);
        await Task.Delay(500, cancellationToken);
        await worker.StopAsync(cancellationToken);

        // Assert
        callCount.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Batch failure: exception stops cascade
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_BatchFails_StopsCascadeGracefully()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "batch-fail";
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, (_, _, _) => throw new InvalidOperationException("db error"));
        MigrationBatchWorker worker = BuildWorker(registry);

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);
        _channel.Writer.Complete();

        // Act
        await worker.StartAsync(cancellationToken);
        await worker.ExecuteTask!;

        // Assert
        worker.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Batch timeout
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_BatchTimeout_StopsCascadeGracefully()
    {
        // Arrange
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string cycleId = "batch-timeout";
        IMigrationCycleRegistry registry = RegistryWith(cycleId, async (_, _, batchCt) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10), batchCt);
            return new MigrationBatchResult(0, null);
        });
        MigrationBatchWorker worker = BuildWorker(
            registry,
            batchTimeout: TimeSpan.FromMilliseconds(100));

        await _channel.Writer.WriteAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100), cancellationToken);
        _channel.Writer.Complete();

        // Act
        await worker.StartAsync(cancellationToken);
        await worker.ExecuteTask!;

        // Assert
        worker.ExecuteTask.IsCompletedSuccessfully.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private sealed class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options);
}
