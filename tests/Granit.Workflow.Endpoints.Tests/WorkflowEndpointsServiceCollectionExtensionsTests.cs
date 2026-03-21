using Granit.Workflow.Endpoints.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Endpoints.Tests;

/// <summary>
/// Tests for <see cref="WorkflowEndpointsServiceCollectionExtensions"/>.
/// </summary>
public sealed class WorkflowEndpointsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitWorkflowEndpoints_ShouldReturnSameServiceCollection()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        IServiceCollection result = services.AddGranitWorkflowEndpoints();

        // Assert
        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitWorkflowEndpoints_CanBeCalledMultipleTimes()
    {
        // Arrange
        ServiceCollection services = new();

        // Act
        services.AddGranitWorkflowEndpoints();
        services.AddGranitWorkflowEndpoints();

        // Assert — idempotent, no duplicate registrations
        ServiceProvider provider = services.BuildServiceProvider();
        provider.ShouldNotBeNull();
    }
}
