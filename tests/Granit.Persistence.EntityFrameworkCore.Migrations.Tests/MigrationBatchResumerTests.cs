// =============================================================================
// Tests — MigrationBatchResumer
// =============================================================================
// Verifies the resume-command dispatch logic: pending and in-progress cycles are
// dispatched, completed cycles are ignored, cursors resume, and tenant fan-out
// produces one command per tenant per cycle. Locking is deliberately absent —
// it is owned by IGranitMigrationRunner (see MigrationStartupServiceTests).
// MigrationProgressDbContext uses the EF Core InMemory provider.
// =============================================================================

using Granit.Commands;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationBatchResumerTests
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
    /// Collects all <see cref="RunMigrationBatchCommand"/> instances dispatched via the sender.
    /// </summary>
    private static List<RunMigrationBatchCommand> GetDispatchedCommands(ICommandSender sender) =>
        sender.ReceivedCalls()
           .Where(c => c.GetMethodInfo().Name == nameof(ICommandSender.SendAsync))
           .Select(c => c.GetArguments()[0])
           .OfType<RunMigrationBatchCommand>()
           .ToList();

    private static MigrationBatchResumer BuildResumer(
        IDbContextFactory<MigrationProgressDbContext> factory,
        ITenantEnumerator tenantEnumerator,
        ICommandSender commandSender,
        int defaultBatchSize = 200)
    {
        // The resumer is a Singleton and creates a scope per dispatch to resolve the
        // Scoped ICommandSender. Wrap the mocked sender in a real IServiceScopeFactory
        // so GetRequiredService<ICommandSender>() inside the resumer returns the mock.
        ServiceCollection services = new();
        services.AddSingleton(commandSender);
        IServiceScopeFactory scopeFactory = services.BuildServiceProvider()
            .GetRequiredService<IServiceScopeFactory>();

        return new(
            factory,
            tenantEnumerator,
            scopeFactory,
            Microsoft.Extensions.Options.Options.Create(new MigrationStartupOptions { DefaultBatchSize = defaultBatchSize }),
            NullLogger<MigrationBatchResumer>.Instance);
    }

    // -------------------------------------------------------------------------
    // No pending cycles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_NoPendingCycles_DoesNotDispatch()
    {
        ICommandSender commandSender = Substitute.For<ICommandSender>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationBatchResumer sut = BuildResumer(CreateFactory(), enumerator, commandSender);

        int dispatched = await sut.ResumeAsync(TestContext.Current.CancellationToken);

        dispatched.ShouldBe(0);
        commandSender.ReceivedCalls().ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Completed and failed rows are ignored
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_OnlyCompletedAndFailedRows_DoesNotDispatch()
    {
        ICommandSender commandSender = Substitute.For<ICommandSender>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "done", Status = MigrationStatus.Completed },
            new() { Id = Guid.NewGuid(), CycleId = "err",  Status = MigrationStatus.Failed    },
        ];

        MigrationBatchResumer sut = BuildResumer(CreateFactory(rows), enumerator, commandSender);

        int dispatched = await sut.ResumeAsync(TestContext.Current.CancellationToken);

        dispatched.ShouldBe(0);
        commandSender.ReceivedCalls().ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Pending row dispatches a command
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_PendingRow_DispatchesOneCommand()
    {
        ICommandSender commandSender = Substitute.For<ICommandSender>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "cycle-a", Status = MigrationStatus.Pending, TenantId = null },
        ];

        MigrationBatchResumer sut = BuildResumer(
            CreateFactory(rows), enumerator, commandSender, defaultBatchSize: 100);

        int dispatched = await sut.ResumeAsync(TestContext.Current.CancellationToken);

        dispatched.ShouldBe(1);
        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(commandSender);
        commands.ShouldHaveSingleItem();
        commands[0].CycleId.ShouldBe("cycle-a");
        commands[0].BatchSize.ShouldBe(100);
    }

    // -------------------------------------------------------------------------
    // InProgress row with cursor resumes from cursor
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_InProgressRowWithCursor_UsesCursorInCommand()
    {
        ICommandSender commandSender = Substitute.For<ICommandSender>();
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

        MigrationBatchResumer sut = BuildResumer(CreateFactory(rows), enumerator, commandSender);

        await sut.ResumeAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(commandSender);
        commands.ShouldHaveSingleItem();
        commands[0].CycleId.ShouldBe("cycle-resume");
        commands[0].Cursor.ShouldBe("{\"lastId\":\"abc\"}");
    }

    // -------------------------------------------------------------------------
    // TenantId = null maps to Guid.Empty
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_NullTenantId_MapsToGuidEmpty()
    {
        ICommandSender commandSender = Substitute.For<ICommandSender>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        MigrationProgress[] rows =
        [
            new() { Id = Guid.NewGuid(), CycleId = "single-tenant", Status = MigrationStatus.Pending, TenantId = null },
        ];

        MigrationBatchResumer sut = BuildResumer(CreateFactory(rows), enumerator, commandSender);

        await sut.ResumeAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(commandSender);
        commands.ShouldHaveSingleItem();
        commands[0].TenantId.ShouldBe(Guid.Empty);
    }

    // -------------------------------------------------------------------------
    // Multi-tenant enumerator — one command per tenant per cycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_TwoTenants_DispatchesTwoCommandsForOneCycle()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        ICommandSender commandSender = Substitute.For<ICommandSender>();
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

        MigrationBatchResumer sut = BuildResumer(CreateFactory(rows), enumerator, commandSender);

        int dispatched = await sut.ResumeAsync(TestContext.Current.CancellationToken);

        List<RunMigrationBatchCommand> commands = GetDispatchedCommands(commandSender);

        // Two commands — one per tenant.
        dispatched.ShouldBe(2);
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
    // Exceptions propagate — error semantics belong to the runner
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ResumeAsync_FactoryThrows_PropagatesException()
    {
        IDbContextFactory<MigrationProgressDbContext> factory =
            Substitute.For<IDbContextFactory<MigrationProgressDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<Task<MigrationProgressDbContext>>(
                _ => throw new InvalidOperationException("db unavailable"));

        ICommandSender commandSender = Substitute.For<ICommandSender>();
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();

        MigrationBatchResumer sut = BuildResumer(factory, enumerator, commandSender);

        // The resumer must NOT swallow: the runner owns retry/exit-code/log semantics,
        // and the startup trigger owns the "never block startup" catch.
        await Should.ThrowAsync<InvalidOperationException>(
            () => sut.ResumeAsync(TestContext.Current.CancellationToken));
    }
}
