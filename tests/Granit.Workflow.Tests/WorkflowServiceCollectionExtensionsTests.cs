using Granit.Workflow.Domain;
using Granit.Workflow.Extensions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="WorkflowServiceCollectionExtensions"/>.
/// </summary>
public sealed class WorkflowServiceCollectionExtensionsTests
{
    // ========================================================================
    // AddGranitWorkflow
    // ========================================================================

    [Fact]
    public void AddGranitWorkflow_ShouldRegisterNullWorkflowPermissionChecker()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflow();

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IWorkflowPermissionChecker? checker = sp.GetService<IWorkflowPermissionChecker>();
        checker.ShouldNotBeNull();
    }

    [Fact]
    public void AddGranitWorkflow_ShouldNotOverrideExistingRegistration()
    {
        // Arrange
        ServiceCollection services = new();
        IWorkflowPermissionChecker customChecker = Substitute.For<IWorkflowPermissionChecker>();
        services.AddScoped(_ => customChecker);

        // Act
        services.AddGranitWorkflow();

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        IWorkflowPermissionChecker resolved = scope.ServiceProvider.GetRequiredService<IWorkflowPermissionChecker>();
        resolved.ShouldBeSameAs(customChecker);
    }

    [Fact]
    public void AddGranitWorkflow_ShouldReturnSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitWorkflow();

        // Assert
        result.ShouldBeSameAs(services);
    }

    // ========================================================================
    // AddWorkflow<TState>
    // ========================================================================

    [Fact]
    public void AddWorkflow_ShouldRegisterDefinitionAsSingleton()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddGranitWorkflow();

        var definition =
            WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
                .InitialState(WorkflowLifecycleStatus.Draft)
                .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published));

        // Act
        services.AddWorkflow(definition);

        using ServiceProvider sp = services.BuildServiceProvider();

        // Assert
        IWorkflowDefinition<WorkflowLifecycleStatus>? resolved =
            sp.GetService<IWorkflowDefinition<WorkflowLifecycleStatus>>();
        resolved.ShouldNotBeNull();
        resolved.ShouldBeSameAs(definition);
    }

    [Fact]
    public void AddWorkflow_ShouldRegisterWorkflowManagerAsScoped()
    {
        // Arrange
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddGranitWorkflow();

        var definition =
            WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
                .InitialState(WorkflowLifecycleStatus.Draft)
                .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published));

        // Act
        services.AddWorkflow(definition);

        using ServiceProvider sp = services.BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // Assert
        IWorkflowManager<WorkflowLifecycleStatus>? manager =
            scope.ServiceProvider.GetService<IWorkflowManager<WorkflowLifecycleStatus>>();
        manager.ShouldNotBeNull();
    }

    [Fact]
    public void AddWorkflow_ShouldReturnSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();
        var definition =
            WorkflowDefinition<WorkflowLifecycleStatus>.Create(b => b
                .InitialState(WorkflowLifecycleStatus.Draft)
                .Transition(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published));

        // Act
        IServiceCollection result = services.AddWorkflow(definition);

        // Assert
        result.ShouldBeSameAs(services);
    }
}
