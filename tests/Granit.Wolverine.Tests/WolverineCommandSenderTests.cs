// =============================================================================
// Tests - WolverineCommandSender
// =============================================================================
// Verifies that ICommandSender forwards commands to IMessageBus.SendAsync and
// validates its null-argument contract.
// =============================================================================

using Granit.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineCommandSenderTests
{
    private sealed record TestCommand(string Payload);

    [Fact]
    public async Task SendAsync_forwards_command_to_IMessageBus()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus);
        TestCommand command = new("hello");

        await sut.SendAsync(command, TestContext.Current.CancellationToken);

        await bus.Received(1).SendAsync(command);
    }

    [Fact]
    public async Task SendAsync_throws_when_command_is_null()
    {
        IMessageBus bus = Substitute.For<IMessageBus>();
        WolverineCommandSender sut = new(bus);

        await Should.ThrowAsync<ArgumentNullException>(
            () => sut.SendAsync<TestCommand>(null!, TestContext.Current.CancellationToken));

        await bus.DidNotReceiveWithAnyArgs().SendAsync<TestCommand>(default!);
    }
}
