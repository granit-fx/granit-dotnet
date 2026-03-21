using Granit.Core.Events;
using Granit.Persistence.Events;
using Granit.Persistence.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class NullIntegrationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithEvents_CompletesWithoutError()
    {
        NullIntegrationEventDispatcher dispatcher = new();

        IIntegrationEvent fakeEvent = Substitute.For<IIntegrationEvent>();
        List<IIntegrationEvent> events = [fakeEvent];

        Func<Task> act = () => dispatcher.DispatchAsync(events, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DispatchAsync_WithEmptyList_CompletesWithoutError()
    {
        NullIntegrationEventDispatcher dispatcher = new();

        Func<Task> act = () => dispatcher.DispatchAsync([], TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public void AddGranitPersistence_RegistersNullIntegrationEventDispatcher_ByDefault()
    {
        ServiceCollection services = new();

        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        IIntegrationEventDispatcher dispatcher = sp.GetRequiredService<IIntegrationEventDispatcher>();
        dispatcher.ShouldBeOfType<NullIntegrationEventDispatcher>();
    }

    [Fact]
    public void AddGranitPersistence_DoesNotOverrideExistingIntegrationEventDispatcher()
    {
        ServiceCollection services = new();
        IIntegrationEventDispatcher custom = Substitute.For<IIntegrationEventDispatcher>();
        services.AddSingleton(custom);

        services.AddGranitPersistence();

        using ServiceProvider sp = services.BuildServiceProvider();
        IIntegrationEventDispatcher resolved = sp.GetRequiredService<IIntegrationEventDispatcher>();
        resolved.ShouldBeSameAs(custom);
    }
}
