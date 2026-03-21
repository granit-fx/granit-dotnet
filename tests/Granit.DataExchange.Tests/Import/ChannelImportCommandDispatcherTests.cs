using System.Threading.Channels;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import;

public sealed class ChannelImportCommandDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WritesCommandToChannel()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ExecuteImportCommand>();
        ChannelImportCommandDispatcher dispatcher = new(channel);
        ExecuteImportCommand command = new(Guid.NewGuid(), "Test.Import");

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        bool read = channel.Reader.TryRead(out ExecuteImportCommand? received);
        read.ShouldBeTrue();
        received.ShouldBe(command);
    }

    [Fact]
    public async Task DispatchAsync_MultipleCommands_AllWritten()
    {
        // Arrange
        var channel = Channel.CreateUnbounded<ExecuteImportCommand>();
        ChannelImportCommandDispatcher dispatcher = new(channel);
        ExecuteImportCommand command1 = new(Guid.NewGuid(), "Test.Import1");
        ExecuteImportCommand command2 = new(Guid.NewGuid(), "Test.Import2");

        // Act
        await dispatcher.DispatchAsync(command1, TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(command2, TestContext.Current.CancellationToken);

        // Assert
        channel.Reader.TryRead(out ExecuteImportCommand? received1);
        channel.Reader.TryRead(out ExecuteImportCommand? received2);
        received1.ShouldBe(command1);
        received2.ShouldBe(command2);
    }
}
