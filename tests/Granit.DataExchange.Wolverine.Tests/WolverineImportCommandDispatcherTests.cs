using Granit.DataExchange.Import.Messages;
using Granit.DataExchange.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Wolverine;
using Xunit;

namespace Granit.DataExchange.Wolverine.Tests;

public sealed class WolverineImportCommandDispatcherTests
{
    private readonly IMessageBus _messageBus = Substitute.For<IMessageBus>();

    private WolverineImportCommandDispatcher CreateDispatcher()
    {
        ServiceCollection services = new();
        services.AddScoped(_ => _messageBus);
        ServiceProvider sp = services.BuildServiceProvider();
        IServiceScopeFactory scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        return new WolverineImportCommandDispatcher(new WolverineScopedSender(scopeFactory));
    }

    [Fact]
    public async Task DispatchAsync_sends_command_via_message_bus()
    {
        // Arrange
        WolverineImportCommandDispatcher dispatcher = CreateDispatcher();
        ExecuteImportCommand command = new(Guid.NewGuid(), "Test.Import");

        // Act
        await dispatcher.DispatchAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await _messageBus.Received(1).SendAsync(command, Arg.Any<DeliveryOptions?>());
    }
}
