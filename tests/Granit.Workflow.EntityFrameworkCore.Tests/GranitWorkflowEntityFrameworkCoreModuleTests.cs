using Granit.Core.Modularity;
using Granit.Persistence;
using Shouldly;
using Xunit;

namespace Granit.Workflow.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for <see cref="GranitWorkflowEntityFrameworkCoreModule"/>.
/// </summary>
public sealed class GranitWorkflowEntityFrameworkCoreModuleTests
{
    [Fact]
    public void Module_ShouldInheritFromGranitModule()
    {
        // Arrange & Act
        GranitWorkflowEntityFrameworkCoreModule module = new();

        // Assert
        module.ShouldBeAssignableTo<GranitModule>();
    }

    [Fact]
    public void Module_ShouldDependOnGranitWorkflowModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowEntityFrameworkCoreModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitWorkflowModule));
    }

    [Fact]
    public void Module_ShouldDependOnGranitPersistenceModule()
    {
        // Arrange
        var attr = (DependsOnAttribute?)Attribute.GetCustomAttribute(
            typeof(GranitWorkflowEntityFrameworkCoreModule), typeof(DependsOnAttribute));

        // Assert
        attr.ShouldNotBeNull();
        attr.DependedTypes.ShouldContain(typeof(GranitPersistenceModule));
    }
}
