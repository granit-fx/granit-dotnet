using Granit.Events;
using Granit.Persistence.Events;
using Granit.Persistence.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Tests;

public sealed class NullIntegrationEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_WithEvents_CompletesWithoutError()
    {
        NullIntegrationEventDispatcher dispatcher = new(Substitute.For<ILogger<NullIntegrationEventDispatcher>>());

        IIntegrationEvent fakeEvent = Substitute.For<IIntegrationEvent>();
        List<IIntegrationEvent> events = [fakeEvent];

        Func<Task> act = () => dispatcher.DispatchAsync(events, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task DispatchAsync_WithEmptyList_CompletesWithoutError()
    {
        NullIntegrationEventDispatcher dispatcher = new(Substitute.For<ILogger<NullIntegrationEventDispatcher>>());

        Func<Task> act = () => dispatcher.DispatchAsync([], TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public void AddGranitPersistence_RegistersNullIntegrationEventDispatcher_ByDefault()
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddMetrics();

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
