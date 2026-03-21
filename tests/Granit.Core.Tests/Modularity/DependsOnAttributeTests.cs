using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Modularity;

public sealed class DependsOnAttributeTests
{
    [Fact]
    public void Constructor_StoresDependedTypes()
    {
        DependsOnAttribute attribute = new(typeof(string), typeof(int));

        attribute.DependedTypes.Length.ShouldBe(2);
        attribute.DependedTypes.ShouldContain(typeof(string));
        attribute.DependedTypes.ShouldContain(typeof(int));
    }

    [Fact]
    public void Constructor_NoDependencies_HasEmptyArray()
    {
        DependsOnAttribute attribute = new();

        attribute.DependedTypes.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_SingleDependency_StoresCorrectly()
    {
        DependsOnAttribute attribute = new(typeof(GranitModule));

        attribute.DependedTypes.ShouldHaveSingleItem()
            .ShouldBe(typeof(GranitModule));
    }

    [Fact]
    public void Attribute_AllowsMultiple()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(DependsOnAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.AllowMultiple.ShouldBeTrue();
    }

    [Fact]
    public void Attribute_IsInherited()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(DependsOnAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.Inherited.ShouldBeTrue();
    }

    [Fact]
    public void Attribute_TargetsClasses()
    {
        var usage = (AttributeUsageAttribute?)Attribute.GetCustomAttribute(
            typeof(DependsOnAttribute), typeof(AttributeUsageAttribute));

        usage.ShouldNotBeNull();
        usage!.ValidOn.ShouldBe(AttributeTargets.Class);
    }
}
