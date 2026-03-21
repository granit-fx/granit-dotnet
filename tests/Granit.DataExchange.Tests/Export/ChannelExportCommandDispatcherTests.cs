using System.Threading.Channels;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Export.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export;

public sealed class ChannelExportCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WritesCommandToChannel()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ExecuteExportCommand>();
        ChannelExportCommandDispatcher dispatcher = new(channel);
        ExecuteExportCommand command = new(Guid.NewGuid());

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        bool read = channel.Reader.TryRead(out ExecuteExportCommand? received);
        read.ShouldBeTrue();
        received.ShouldBe(command);
    }

    [Fact]
    public async Task DispatchAsync_MultipleCommands_AllWritten()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ExecuteExportCommand>();
        ChannelExportCommandDispatcher dispatcher = new(channel);
        ExecuteExportCommand command1 = new(Guid.NewGuid());
        ExecuteExportCommand command2 = new(Guid.NewGuid());

        // Act
        await dispatcher.DispatchAsync(command1, TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(command2, TestContext.Current.CancellationToken);

        // Assert
        channel.Reader.TryRead(out ExecuteExportCommand? received1);
        channel.Reader.TryRead(out ExecuteExportCommand? received2);
        received1.ShouldBe(command1);
        received2.ShouldBe(command2);
    }
}
