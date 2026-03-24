using Granit.AI;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Workflow.AI.Tests;

/// <summary>
/// Tests for <see cref="GranitWorkflowAIModule"/>.
/// </summary>
public sealed class GranitWorkflowAIModuleTests
{
    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        // Arrange & Act
        GranitWorkflowAIModule module = new();

        // Assert
        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_ShouldDependOnGranitAIModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowAIModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitAIModule));
    }

    [Fact]
    public void Module_ShouldDependOnGranitWorkflowModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowAIModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitWorkflowModule));
    }
}
