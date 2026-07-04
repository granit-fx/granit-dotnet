using Granit.Scheduling;
using Granit.Workflow.Scheduling.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Scheduling.Tests;

public sealed class WorkflowSchedulingRegistrationTests
{
    private sealed class FakeApplier : IWorkflowTransitionApplier
    {
        public Task ApplyAsync(Guid entityId, string targetState, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<IScheduler>());
        services.AddSingleton(Substitute.For<IScheduledActionReader>());
        services.AddWorkflowScheduling();
        services.AddWorkflowTransitionApplier<FakeApplier>("BlogPost");
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddWorkflowScheduling_registers_service_and_registry()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetService<IScheduledTransitionService>().ShouldNotBeNull();
        scope.ServiceProvider.GetService<IWorkflowTransitionApplierRegistry>().ShouldNotBeNull();
    }

    [Fact]
    public void Registry_resolves_applier_registered_for_entity_type()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IWorkflowTransitionApplierRegistry registry = scope.ServiceProvider.GetRequiredService<IWorkflowTransitionApplierRegistry>();

        registry.Resolve("BlogPost").ShouldBeOfType<FakeApplier>();
    }

    [Fact]
    public void Registry_throws_for_unregistered_entity_type()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        IWorkflowTransitionApplierRegistry registry = scope.ServiceProvider.GetRequiredService<IWorkflowTransitionApplierRegistry>();

        Should.Throw<InvalidOperationException>(() => registry.Resolve("Unknown"));
    }

    [Fact]
    public void AddWorkflowTransitionApplier_rejects_blank_entity_type()
    {
        var services = new ServiceCollection();
        Should.Throw<ArgumentException>(() => services.AddWorkflowTransitionApplier<FakeApplier>("  "));
    }
}
