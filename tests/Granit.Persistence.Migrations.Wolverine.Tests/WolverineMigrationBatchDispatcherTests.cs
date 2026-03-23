using Granit.Persistence.Migrations.Messages;
using Granit.Persistence.Migrations.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Wolverine;
using Xunit;

namespace Granit.Persistence.Migrations.Wolverine.Tests;

public sealed class WolverineMigrationBatchDispatcherTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    private WolverineMigrationBatchDispatcher CreateDispatcher()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => _messageBus);
        ServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new WolverineMigrationBatchDispatcher(new WolverineScopedSender(scopeFactory));
    }

    [Fact]
    public async Task DispatchAsync_SingleCommand_SendsViaMessageBus()
    {
        // Arrange
        WolverineMigrationBatchDispatcher dispatcher = CreateDispatcher();
        RunMigrationBatchCommand command = new("cycle-1", Guid.Empty, null, 100);

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(1).SendAsync(command, Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task DispatchAsync_MultipleCommands_SendsAllViaMessageBus()
    {
        // Arrange
        WolverineMigrationBatchDispatcher dispatcher = CreateDispatcher();
        RunMigrationBatchCommand[] commands =
        [
            new("cycle-1", Guid.Empty, null, 100),
            new("cycle-1", Guid.Empty, "cursor-1", 100),
            new("cycle-2", Guid.NewGuid(), null, 50)
        ];

        // Act
        await dispatcher.DispatchAsync(commands, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(3).SendAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
        await _messageBus.Received(1).SendAsync(commands[0], Arg.Any<DeliveryOptions?>());
        await _messageBus.Received(1).SendAsync(commands[1], Arg.Any<DeliveryOptions?>());
        await _messageBus.Received(1).SendAsync(commands[2], Arg.Any<DeliveryOptions?>());
    }

    [Fact]
    public async Task DispatchAsync_EmptyCollection_DoesNotCallMessageBus()
    {
        // Arrange
        WolverineMigrationBatchDispatcher dispatcher = CreateDispatcher();

        // Act
        await dispatcher.DispatchAsync([], TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.DidNotReceive().SendAsync(Arg.Any<object>(), Arg.Any<DeliveryOptions?>());
    }
}
