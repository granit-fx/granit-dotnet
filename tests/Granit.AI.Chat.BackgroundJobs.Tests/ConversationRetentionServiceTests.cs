using Granit.AI.Chat.BackgroundJobs.Internal;
using Granit.AI.Chat.BackgroundJobs.Options;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Chat.BackgroundJobs.Tests;

public sealed class ConversationRetentionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IConversationDataManager _dataManager = Substitute.For<IConversationDataManager>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private ConversationRetentionService Create(GranitAIChatRetentionOptions options)
    {
        _clock.Now.Returns(Now);
        return new ConversationRetentionService(
            _dataManager, MsOptions.Create(options), _clock, NullLogger<ConversationRetentionService>.Instance);
    }

    [Fact]
    public async Task Does_nothing_when_retention_is_disabled()
    {
        ConversationRetentionService service = Create(new GranitAIChatRetentionOptions { RetentionDays = 0 });

        await service.ExecuteAsync(TestContext.Current.CancellationToken);

        await _dataManager.DidNotReceive().PurgeOlderThanAsync(
            Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Purges_with_the_cutoff_derived_from_the_retention_window()
    {
        ConversationRetentionService service = Create(
            new GranitAIChatRetentionOptions { RetentionDays = 30, CleanupBatchSize = 250 });

        await service.ExecuteAsync(TestContext.Current.CancellationToken);

        await _dataManager.Received(1).PurgeOlderThanAsync(
            Now - TimeSpan.FromDays(30), 250, Arg.Any<CancellationToken>());
    }
}
