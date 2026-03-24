using Granit.Modularity;
using Granit.Webhooks;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Wolverine.Tests;

public sealed class GranitWebhooksWolverineModuleTests
{
    [Fact]
    public void DependsOn_declares_webhooks_and_wolverine_modules()
    {
        // Arrange & Act
        DependsOnAttribute? attribute = typeof(GranitWebhooksWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        // Assert
        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitWebhooksModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_replaces_webhook_publisher()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IWebhookPublisher, StubWebhookPublisher>();
        builder.Services.AddSingleton<IWebhookCommandDispatcher, StubWebhookCommandDispatcher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitWebhooksWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IWebhookPublisher));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineWebhookPublisher));
    }

    [Fact]
    public void ConfigureServices_replaces_webhook_command_dispatcher()
    {
        // Arrange
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IWebhookPublisher, StubWebhookPublisher>();
        builder.Services.AddSingleton<IWebhookCommandDispatcher, StubWebhookCommandDispatcher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        GranitWebhooksWolverineModule module = new();

        // Act
        module.ConfigureServices(context);

        // Assert
        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IWebhookCommandDispatcher));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineWebhookCommandDispatcher));
    }

    private sealed class StubWebhookPublisher : IWebhookPublisher
    {
        public ValueTask PublishAsync<TPayload>(
            string eventType,
            TPayload payload,
            CancellationToken cancellationToken = default) where TPayload : notnull =>
            ValueTask.CompletedTask;
    }

    private sealed class StubWebhookCommandDispatcher : IWebhookCommandDispatcher
    {
        public Task DispatchAsync(SendWebhookCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
