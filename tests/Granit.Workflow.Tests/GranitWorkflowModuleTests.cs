using Granit.Modularity;
using Granit.QueryEngine;
using Granit.Timing;
using Shouldly;
using Xunit;

namespace Granit.Workflow.Tests;

/// <summary>
/// Tests for <see cref="GranitWorkflowModule"/>.
/// </summary>
public sealed class GranitWorkflowModuleTests
{
    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        // Arrange & Act
        GranitWorkflowModule module = new();

        // Assert
        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_ShouldDependOnGranitQueryEngineModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitQueryEngineModule));
    }

    [Fact]
    public void Module_ShouldDependOnGranitTimingModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitTimingModule));
    }
}
