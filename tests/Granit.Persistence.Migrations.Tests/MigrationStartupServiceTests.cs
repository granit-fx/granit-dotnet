// =============================================================================
// Tests — MigrationStartupService
// =============================================================================
// Verifies that pending and in-progress cycles are resumed at startup, that
// completed cycles are ignored, and that exceptions do not block startup.
// MigrationProgressDbContext uses the EF Core InMemory provider.
// =============================================================================

using Granit.Persistence.Migrations.Internal;
using Granit.Persistence.Migrations.Messages;
using Granit.Persistence.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Migrations.Tests;

public sealed class MigrationStartupServiceTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a real <see cref="IDbContextFactory{TContext}"/> backed by EF Core InMemory,
    /// pre-seeded with the given rows. Uses a real service provider so all contexts
    /// from the factory share the same in-memory database service.
    /// </summary>
    private static IDbContextFactory<MigrationProgressDbContext> CreateFactory(
        params MigrationProgress[] rows)
    {
        string dbName = Guid.NewGuid().ToString();
        ServiceCollection services = new();
        services.AddDbContextFactory<MigrationProgressDbContext>(
            opts => opts.UseInMemoryDatabase(dbName));
        ServiceProvider sp = services.BuildServiceProvider();

        IDbContextFactory<MigrationProgressDbContext> factory =
            sp.GetRequiredService<IDbContextFactory<MigrationProgressDbContext>>();

        if (rows.Length > 0)
        {
            using MigrationProgressDbContext seed = factory.CreateDbContext();
            seed.MigrationProgresses.AddRange(rows);
            seed.SaveChanges();
        }

        return factory;
    }

    /// <summary>
    /// Returns an <see cref="IAsyncEnumerable{T}"/> that yields the given tenant identifiers.
    /// </summary>
    private static async IAsyncEnumerable<Guid> ToAsyncEnumerable(params Guid[] tenantIds)
    {
        foreach (Guid id in tenantIds)
        {
            yield return id;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Collects all <see cref="RunMigrationBatchCommand"/> instances dispatched via the dispatcher.
    /// </summary>
    private static List<RunMigrationBatchCommand> GetDispatchedCommands(IMigrationBatchDispatcher dispatcher) =>
        dispatcher.ReceivedCalls()
           .Where(c => c.GetMethodInfo().Name == nameof(IMigrationBatchDispatcher.DispatchAsync))
           .SelectMany(c =>
           {
               object? arg = c.GetArguments()[0];
               if (arg is IEnumerable<RunMigrationBatchCommand> commands)
               {
                   return commands;
               }

               if (arg is RunMigrationBatchCommand single)
               {
                   return [single];
               }

               return [];
           })
           .ToList();

    private static MigrationStartupService BuildService(
        IDbContextFactory<MigrationProgressDbContext> factory,
        ITenantEnumerator tenantEnumerator,
        IMigrationBatchDispatcher dispatcher,
        int defaultBatchSize = 200) =>
        new(
            factory,
            Substitute.For<IMigrationProgressDbEnsurer>(),
            tenantEnumerator,
            dispatcher,
            Microsoft.Extensions.Options.Options.Create(new MigrationStartupOptions { DefaultBatchSize = defaultBatchSize }),
            NullLogger<MigrationStartupService>.Instance);

    // -------------------------------------------------------------------------
    // No pending cycles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_NoPendingCycles_DoesNotDispatch()
    {
        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationStartupService sut = BuildService(CreateFactory(), enumerator, dispatcher);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Completed and failed rows are ignored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_OnlyCompletedAndFailedRows_DoesNotDispatch()
    {
        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "done", Status = MigrationStatus.Completed },
            new() { Id = Guid.NewGuid(), CycleId = "err",  Status = MigrationStatus.Failed    },
        ];

        MigrationStartupService sut = BuildService(CreateFactory(rows), enumerator, dispatcher);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        dispatcher.ReceivedCalls().ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Pending row dispatches a command
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_PendingRow_DispatchesOneCommand()
    {
        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "cycle-a", Status = MigrationStatus.Pending, TenantId = null },
        ];

        MigrationStartupService sut = BuildService(CreateFactory(rows), enumerator, dispatcher, defaultBatchSize: 100);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(dispatcher);
        commands.ShouldHaveSingleItem();
        commands[0].CycleId.ShouldBe("cycle-a");
        commands[0].BatchSize.ShouldBe(100);
    }

    // -------------------------------------------------------------------------
    // InProgress row with cursor resumes from cursor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_InProgressRowWithCursor_UsesCursorInCommand()
    {
        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new()
            {
                Id         = Guid.NewGuid(),
                CycleId    = "cycle-resume",
                Status     = MigrationStatus.InProgress,
                LastCursor = "{\"lastId\":\"abc\"}",
                TenantId   = null,
            },
        ];

        MigrationStartupService sut = BuildService(CreateFactory(rows), enumerator, dispatcher);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(dispatcher);
        commands.ShouldHaveSingleItem();
        commands[0].CycleId.ShouldBe("cycle-resume");
        commands[0].Cursor.ShouldBe("{\"lastId\":\"abc\"}");
    }

    // -------------------------------------------------------------------------
    // TenantId = null maps to Guid.Empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_NullTenantId_MapsToGuidEmpty()
    {
        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "single-tenant", Status = MigrationStatus.Pending, TenantId = null },
        ];

        MigrationStartupService sut = BuildService(CreateFactory(rows), enumerator, dispatcher);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(dispatcher);
        commands.ShouldHaveSingleItem();
        commands[0].TenantId.ShouldBe(Guid.Empty);
    }

    // -------------------------------------------------------------------------
    // Multi-tenant enumerator — one command per tenant per cycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_TwoTenants_DispatchesTwoCommandsForOneCycle()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(tenantA, tenantB));

        MigrationProgress[] rows =
        [
            new()
            {
                Id         = Guid.NewGuid(),
                CycleId    = "schema-cycle",
                Status     = MigrationStatus.Pending,
                TenantId   = tenantA,
                LastCursor = "cursor-a",
            },
        ];

        MigrationStartupService sut = BuildService(CreateFactory(rows), enumerator, dispatcher);

        await sut.StartAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(dispatcher);

        // Two commands — one per tenant.
        commands.Count.ShouldBe(2);

        // Tenant A reuses its stored cursor.
        RunMigrationBatchCommand commandA = commands.Single(c => c.TenantId == tenantA);
        commandA.CycleId.ShouldBe("schema-cycle");
        commandA.Cursor.ShouldBe("cursor-a");

        // Tenant B has no stored row → null cursor (start from beginning).
        RunMigrationBatchCommand commandB = commands.Single(c => c.TenantId == tenantB);
        commandB.CycleId.ShouldBe("schema-cycle");
        commandB.Cursor.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Exception does not block startup
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_FactoryThrows_DoesNotPropagateException()
    {
        IDbContextFactory<MigrationProgressDbContext> factory =
            Substitute.For<IDbContextFactory<MigrationProgressDbContext>>();
        factory.CreateDbContext().Returns(_ => throw new InvalidOperationException("db unavailable"));

        IMigrationBatchDispatcher dispatcher = Substitute.For<IMigrationBatchDispatcher>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();

        MigrationStartupService sut = BuildService(factory, enumerator, dispatcher);

        Func<Task> act = () => sut.StartAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }
}
