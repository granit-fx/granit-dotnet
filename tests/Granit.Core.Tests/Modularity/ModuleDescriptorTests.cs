using Granit.Core.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Core.Tests.Modularity;

public sealed class ModuleDescriptorTests
{
    [Fact]
    public void Constructor_SetsModuleType()
    {
        TestModule instance = new();
        ModuleDescriptor descriptor = new(typeof(TestModule), instance, []);

        descriptor.ModuleType.ShouldBe(typeof(TestModule));
    }

    [Fact]
    public void Constructor_SetsInstance()
    {
        TestModule instance = new();
        ModuleDescriptor descriptor = new(typeof(TestModule), instance, []);

        descriptor.Instance.ShouldBeSameAs(instance);
    }

    [Fact]
    public void Constructor_SetsDependencies()
    {
        TestModule instance = new();
        Type[] deps = [typeof(string), typeof(int)];
        ModuleDescriptor descriptor = new(typeof(TestModule), instance, deps);

        descriptor.Dependencies.ShouldBe(deps);
    }

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        TestModule instance = new();
        ModuleDescriptor descriptor = new(typeof(TestModule), instance, []);

        descriptor.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_CanBeSetToFalse()
    {
        TestModule instance = new();
        ModuleDescriptor descriptor = new(typeof(TestModule), instance, []);

        descriptor.IsEnabled = false;

        descriptor.IsEnabled.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Test fixture
    // -------------------------------------------------------------------------

    private sealed class TestModule : GranitModule;
}
