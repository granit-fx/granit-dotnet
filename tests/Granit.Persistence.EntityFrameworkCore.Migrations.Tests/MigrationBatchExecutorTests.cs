// =============================================================================
// Tests — MigrationBatchExecutor
// =============================================================================
// Verifies the cascade logic, progress lifecycle, and error handling of the
// transport-agnostic batch executor without requiring a real database.
// MigrationProgressDbContext uses the EF Core InMemory provider.
// =============================================================================

using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationBatchExecutorTests : IDisposable
{
    private readonly MigrationProgressDbContext _progressContext;
    private readonly ITenantDbIsolator _isolator;
    private readonly IClock _clock;

    public MigrationBatchExecutorTests()
    {
        DbContextOptions<MigrationProgressDbContext> options =
            new DbContextOptionsBuilder<MigrationProgressDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

        _progressContext = new MigrationProgressDbContext(options);
        _isolator = Substitute.For<ITenantDbIsolator>();
        _clock = Substitute.For<IClock>();
        _clock.Now.Returns(DateTimeOffset.UtcNow);
    }

    public void Dispose() => _progressContext.Dispose();

    // Builds a registry that returns the given registration for cycleId.
    private static IMigrationCycleRegistry RegistryWith(
        string cycleId, Type dbContextType, BatchMigrationDelegate migration)
    {
        IMigrationCycleRegistry registry = Substitute.For<IMigrationCycleRegistry>();
        registry.Find(cycleId).Returns(new MigrationCycleRegistration(cycleId, dbContextType, migration));
        return registry;
    }

    // Builds a service provider that has TContext registered.
    private static ServiceProvider ProviderWith<TContext>(TContext context)
        where TContext : DbContext
    {
        ServiceCollection services = new();
        services.AddSingleton(context);
        return services.BuildServiceProvider();
    }

    private MigrationBatchExecutor BuildExecutor(
        IMigrationCycleRegistry registry,
        IServiceProvider serviceProvider) =>
        new(
            registry,
            serviceProvider,
            _progressContext,
            _isolator,
            _clock,
            new SimpleGuidGenerator(),
            NullLogger<MigrationBatchExecutor>.Instance);

    // -------------------------------------------------------------------------
    // Unknown cycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_UnknownCycle_ReturnsNull()
    {
        IMigrationCycleRegistry registry = Substitute.For<IMigrationCycleRegistry>();
        registry.Find(Arg.Any<string>()).Returns((MigrationCycleRegistration?)null);
        MigrationBatchExecutor executor = BuildExecutor(registry, new ServiceCollection().BuildServiceProvider());

        RunMigrationBatchCommand? result = await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand("missing", Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Already-completed cycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_AlreadyCompleted_ReturnsNull()
    {
        const string cycleId = "completed-cycle";
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(10, null)));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        _progressContext.MigrationProgresses.Add(new MigrationProgress
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            Phase = MigrationPhase.Migrate,
            Status = MigrationStatus.Completed,
            TenantId = null,
        });
        await _progressContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        RunMigrationBatchCommand? result = await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Last batch (no next cursor) → migration complete
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_LastBatch_ReturnsNullAndMarksCompleted()
    {
        const string cycleId = "last-batch";
        StubDbContext stubContext = CreateStubContext();
        DateTimeOffset completedAt = DateTimeOffset.UtcNow;
        _clock.Now.Returns(completedAt);

        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(42, null)));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        RunMigrationBatchCommand? result = await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldBeNull();

        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress.ShouldNotBeNull();
        progress!.Status.ShouldBe(MigrationStatus.Completed);
        progress.ProcessedRows.ShouldBe(42);
        progress.CompletedAt.ShouldBe(completedAt);
    }

    // -------------------------------------------------------------------------
    // Mid-batch (has next cursor) → cascade
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_MidBatch_ReturnsNextCommand()
    {
        const string cycleId = "mid-batch";
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(100, "{\"lastId\":999}")));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        RunMigrationBatchCommand? result = await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result!.CycleId.ShouldBe(cycleId);
        result.Cursor.ShouldBe("{\"lastId\":999}");
        result.BatchSize.ShouldBe(100);
        result.TenantId.ShouldBe(Guid.Empty);
    }

    // -------------------------------------------------------------------------
    // Accumulated row count across two consecutive batches
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_AccumulatesProcessedRows_AcrossBatches()
    {
        const string cycleId = "accumulate";
        StubDbContext stubContext = CreateStubContext();
        int callCount = 0;

        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) =>
            {
                callCount++;
                string? next = callCount == 1 ? "cursor-2" : null;
                return Task.FromResult(new MigrationBatchResult(50, next));
            });
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        RunMigrationBatchCommand? result1 = await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);
        result1.ShouldNotBeNull();

        RunMigrationBatchCommand? result2 = await executor.ExecuteBatchAsync(
            result1!,
            TestContext.Current.CancellationToken);
        result2.ShouldBeNull();

        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress!.ProcessedRows.ShouldBe(100);
        progress.Status.ShouldBe(MigrationStatus.Completed);
    }

    // -------------------------------------------------------------------------
    // Tenant isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_NonEmptyTenantId_CallsIsolator()
    {
        const string cycleId = "tenant-isolation";
        var tenantId = Guid.NewGuid();
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null)));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, tenantId, null, 100),
            TestContext.Current.CancellationToken);

        await _isolator.Received(1).IsolateAsync(
            Arg.Any<DbContext>(), tenantId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteBatchAsync_EmptyTenantId_DoesNotCallIsolator()
    {
        const string cycleId = "no-tenant";
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null)));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        await _isolator.DidNotReceive().IsolateAsync(
            Arg.Any<DbContext>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // TenantId → null mapping in progress entity
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_EmptyTenantId_StoresNullTenantIdInProgress()
    {
        const string cycleId = "null-tenant";
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => Task.FromResult(new MigrationBatchResult(0, null)));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        await executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress.ShouldNotBeNull();
        progress!.TenantId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Error handling
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_BatchThrows_MarksFailedAndRethrows()
    {
        const string cycleId = "failing-batch";
        StubDbContext stubContext = CreateStubContext();
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => throw new InvalidOperationException("boom"));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        Func<Task> act = () => executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            TestContext.Current.CancellationToken);

        (await Should.ThrowAsync<InvalidOperationException>(act)).Message.ShouldBe("boom");

        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress.ShouldNotBeNull();
        progress!.Status.ShouldBe(MigrationStatus.Failed);
        progress.Error.ShouldBe("boom");
    }

    [Fact]
    public async Task ExecuteBatchAsync_BatchThrowsOperationCanceled_PropagatesWithoutMarkingFailed()
    {
        const string cycleId = "cancel-batch";
        StubDbContext stubContext = CreateStubContext();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(new MigrationBatchResult(0, null));
            });
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        Func<Task> act = () => executor.ExecuteBatchAsync(
            new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
            cts.Token);

        await Should.ThrowAsync<OperationCanceledException>(act);

        // Progress should NOT be marked as Failed for cancellation.
        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Error message truncation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteBatchAsync_LongErrorMessage_TruncatesTo4000Chars()
    {
        const string cycleId = "long-error";
        StubDbContext stubContext = CreateStubContext();
        string longMessage = new('x', 5000);
        IMigrationCycleRegistry registry = RegistryWith(
            cycleId, typeof(StubDbContext),
            (_, _, _) => throw new InvalidOperationException(longMessage));
        MigrationBatchExecutor executor = BuildExecutor(registry, ProviderWith(stubContext));

        try
        {
            await executor.ExecuteBatchAsync(
                new RunMigrationBatchCommand(cycleId, Guid.Empty, null, 100),
                TestContext.Current.CancellationToken);
        }
        catch (InvalidOperationException) { /* expected */ }

        MigrationProgress? progress = await _progressContext.MigrationProgresses
            .FirstOrDefaultAsync(p => p.CycleId == cycleId, TestContext.Current.CancellationToken);
        progress!.Error!.Length.ShouldBe(4000);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static StubDbContext CreateStubContext() =>
        new(new DbContextOptionsBuilder<StubDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class StubDbContext(DbContextOptions<StubDbContext> options) : DbContext(options);
}
