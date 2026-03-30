// =============================================================================
// Tests — ChannelBatchDispatcher
// =============================================================================
// Verifies that the Channel-based dispatcher writes commands to the channel
// so that MigrationBatchWorker can consume them.
// =============================================================================

using System.Threading.Channels;
using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class ChannelBatchDispatcherTests
{
    private readonly Channel<RunMigrationBatchCommand> _channel = Channel.CreateUnbounded<RunMigrationBatchCommand>();

    private ChannelBatchDispatcher CreateDispatcher() => new(_channel);

    [Fact]
    public async Task DispatchAsync_SingleCommand_WritesToChannel()
    {
        // Arrange
        ChannelBatchDispatcher dispatcher = CreateDispatcher();
        RunMigrationBatchCommand command = new("cycle-1", Guid.Empty, null, 100);

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        bool hasItem = _channel.Reader.TryRead(out RunMigrationBatchCommand? read);
        hasItem.ShouldBeTrue();
        read.ShouldBe(command);
    }

    [Fact]
    public async Task DispatchAsync_MultipleCommands_WritesAllToChannel()
    {
        // Arrange
        ChannelBatchDispatcher dispatcher = CreateDispatcher();
        RunMigrationBatchCommand[] commands =
        [
            new("cycle-1", Guid.Empty, null, 100),
            new("cycle-1", Guid.Empty, "cursor-1", 100),
            new("cycle-2", Guid.NewGuid(), null, 50)
        ];

        // Act
        await dispatcher.DispatchAsync(commands, TestContext.Current.CancellationToken);

        // Assert
        List<RunMigrationBatchCommand> received = [];
        while (_channel.Reader.TryRead(out RunMigrationBatchCommand? item))
        {
            received.Add(item);
        }

        received.Count.ShouldBe(3);
        received.ShouldBe(commands);
    }

    [Fact]
    public async Task DispatchAsync_EmptyCollection_WritesNothing()
    {
        // Arrange
        ChannelBatchDispatcher dispatcher = CreateDispatcher();

        // Act
        await dispatcher.DispatchAsync([], TestContext.Current.CancellationToken);

        // Assert
        bool hasItem = _channel.Reader.TryRead(out _);
        hasItem.ShouldBeFalse();
    }

    [Fact]
    public async Task DispatchAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        ChannelBatchDispatcher dispatcher = CreateDispatcher();
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        RunMigrationBatchCommand command = new("cycle-1", Guid.Empty, null, 100);

        // Act & Assert
        Func<Task> act = () => dispatcher.DispatchAsync(command, cts.Token);
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
