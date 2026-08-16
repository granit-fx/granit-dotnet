using System.Diagnostics.Metrics;
using System.Threading.Channels;
using Granit.Auditing.BackgroundJobs.Jobs;
using Granit.BackgroundJobs;
using Granit.BackgroundJobs.Abstractions;
using Granit.BackgroundJobs.Diagnostics;
using Granit.BackgroundJobs.Internal;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Auditing.BackgroundJobs.Tests;

/// <summary>
/// Drives this satellite's recurring job through the in-process channel path — dispatcher,
/// channel, worker, handler — with no Wolverine runtime, i.e. the configuration
/// <c>GranitBackgroundJobsModule</c> wires by default.
/// </summary>
/// <remarks>
/// Regression cover for #3206: the worker used to demand a <c>{MessageTypeName}Handler</c>
/// type, which no satellite ships (they ship <c>{Action}Handler</c> per CLAUDE.md), so every
/// recurring job failed on the documented default. The tests in
/// <c>Granit.BackgroundJobs.Tests</c> could not catch it because their fixtures lived in the
/// worker's own test assembly and happened to follow the stricter name; this one binds a job
/// and a handler that really ship together in a satellite package.
/// </remarks>
public sealed class SatelliteJobChannelPathTests
{
    [Fact]
    public async Task ChannelPath_AuditRetentionCleanupJob_InvokesTheSatelliteHandler()
    {
        IAuditRetentionCleanupService cleanup = Substitute.For<IAuditRetentionCleanupService>();
        IBackgroundJobStoreWriter storeWriter = Substitute.For<IBackgroundJobStoreWriter>();
        IBackgroundJobStoreReader storeReader = Substitute.For<IBackgroundJobStoreReader>();

        var channel = Channel.CreateUnbounded<BackgroundJobEnvelope>();
        ChannelBackgroundJobDispatcher dispatcher = new(channel, TimeProvider.System);

        ServiceCollection services = new();
        services.AddSingleton(cleanup);
        services.AddSingleton(storeWriter);
        services.AddSingleton(storeReader);
        services.AddSingleton<IBackgroundJobDispatcher>(dispatcher);
        services.AddMetrics();
        await using ServiceProvider provider = services.BuildServiceProvider();

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(new DateTimeOffset(2026, 3, 1, 2, 0, 0, TimeSpan.Zero));

        BackgroundJobsMetrics metrics = new(provider.GetRequiredService<IMeterFactory>());
        BackgroundJobWorker worker = new(
            channel,
            provider.GetRequiredService<IServiceScopeFactory>(),
            clock,
            metrics,
            NullLogger<BackgroundJobWorker>.Instance);

        // AuditRetentionCleanupJob carries [RecurringJob], so the worker takes the recurring
        // path: store writes around the run, then a reschedule that stops at the substituted
        // reader returning no definition.
        await dispatcher.PublishAsync(
            new AuditRetentionCleanupJob(),
            cancellationToken: TestContext.Current.CancellationToken);
        channel.Writer.Complete();

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await WaitUntilAsync(() => cleanup.ReceivedCalls().Any());
        await worker.StopAsync(TestContext.Current.CancellationToken);

        await cleanup.Received(1).ExecuteAsync(Arg.Any<CancellationToken>());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (int attempt = 0; attempt < 50 && !condition(); attempt++)
        {
            await Task.Delay(20);
        }
    }
}
