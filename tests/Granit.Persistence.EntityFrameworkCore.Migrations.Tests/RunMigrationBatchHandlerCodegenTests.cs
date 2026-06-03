using Granit.Persistence.EntityFrameworkCore.Migrations.Handlers;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Shouldly;
using Wolverine;
using Wolverine.Tracking;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

// Codegen + behaviour coverage for the RunMigrationBatchHandler cascade.
//
// The handler returns RunMigrationBatchCommand? (the next batch) instead of dispatching it
// via ICommandSender / a separately-injected IMessageBus. Booting a real Wolverine host and
// invoking the command with activity tracking proves three things at once: the handler is
// discovered, its Task<RunMigrationBatchCommand?> signature compiles, and the return value is
// re-published as a cascading message. Cascading is what enrols the follow-up command in the
// same handler transaction/outbox under AutoApplyTransactions (production) — a guarantee the
// previous injected-IMessageBus dispatch did not provide.
public sealed class RunMigrationBatchHandlerCodegenTests
{
    private static readonly RunMigrationBatchCommand First = new("cycle-1", Guid.Empty, Cursor: null, BatchSize: 100);
    private static readonly RunMigrationBatchCommand Next = First with { Cursor = "page-2" };

    [Fact]
    public async Task Handler_cascades_followup_batch_until_executor_returns_null()
    {
        IMigrationBatchExecutor executor = Substitute.For<IMigrationBatchExecutor>();
        // First batch yields a follow-up; the follow-up batch yields nothing → cascade stops.
        executor.ExecuteBatchAsync(Arg.Any<RunMigrationBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns(Next, (RunMigrationBatchCommand?)null);

        using IHost host = await BuildHostAsync(executor);

        ITrackedSession session = await host.TrackActivity().InvokeMessageAndWaitAsync(First);

        // Two RunMigrationBatchCommand executed: the invoked one + the cascaded follow-up.
        // The cascaded message proves the return value flows through the handler's outgoing
        // (outbox) pipeline, not a separate non-transactional dispatch.
        session.Executed.MessagesOf<RunMigrationBatchCommand>().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Handler_stops_when_no_rows_remain()
    {
        IMigrationBatchExecutor executor = Substitute.For<IMigrationBatchExecutor>();
        executor.ExecuteBatchAsync(Arg.Any<RunMigrationBatchCommand>(), Arg.Any<CancellationToken>())
            .Returns((RunMigrationBatchCommand?)null);

        using IHost host = await BuildHostAsync(executor);

        ITrackedSession session = await host.TrackActivity().InvokeMessageAndWaitAsync(First);

        // Only the invoked command runs — a null return cascades nothing.
        session.Executed.MessagesOf<RunMigrationBatchCommand>().Count().ShouldBe(1);
    }

    private static Task<IHost> BuildHostAsync(IMigrationBatchExecutor executor) =>
        Host.CreateDefaultBuilder()
            .ConfigureServices(services => services.AddSingleton(executor))
            .UseWolverine(opts =>
                opts.ApplicationAssembly = typeof(RunMigrationBatchHandler).Assembly)
            .StartAsync();
}
