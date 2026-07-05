using Granit.Workspaces.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Workspaces.Abstractions.Tests;

public sealed class WorkspaceServiceCollectionExtensionsTests
{
    [Fact]
    public void AddWorkspaceDefinition_registers_concrete_base_and_descriptor()
    {
        ServiceCollection services = new();
        services.AddWorkspaceDefinition<DummyWorkspace>();

        using ServiceProvider sp = services.BuildServiceProvider();
        DummyWorkspace concrete = sp.GetRequiredService<DummyWorkspace>();
        WorkspaceDefinition asBase = sp.GetRequiredService<WorkspaceDefinition>();
        IWorkspaceDescriptor asDescriptor = sp.GetRequiredService<IWorkspaceDescriptor>();

        ReferenceEquals(concrete, asBase).ShouldBeTrue();
        ReferenceEquals(concrete, asDescriptor).ShouldBeTrue();
    }

    [Fact]
    public void AddWorkspaceDefinition_throws_when_services_null() =>
        Should.Throw<ArgumentNullException>(() =>
            WorkspaceServiceCollectionExtensions.AddWorkspaceDefinition<DummyWorkspace>(null!));

    [Fact]
    public void AddFeatureProvider_registers_as_singleton()
    {
        ServiceCollection services = new();
        services.AddFeatureProvider<DummyFeatureProvider>();

        using ServiceProvider sp = services.BuildServiceProvider();
        IFeatureProvider a = sp.GetRequiredService<IFeatureProvider>();
        IFeatureProvider b = sp.GetRequiredService<IFeatureProvider>();

        ReferenceEquals(a, b).ShouldBeTrue();
        a.ShouldBeOfType<DummyFeatureProvider>();
    }

    [Fact]
    public void AddFeatureProvider_throws_when_services_null() =>
        Should.Throw<ArgumentNullException>(() =>
            WorkspaceServiceCollectionExtensions.AddFeatureProvider<DummyFeatureProvider>(null!));

    private sealed class DummyWorkspace : WorkspaceDefinition
    {
        public override string Name => "Dummy";
        protected override void Configure(WorkspaceBuilder builder) { }
    }

    private sealed class DummyFeatureProvider : IFeatureProvider
    {
        public void DefineFeatures(IFeatureCatalogBuilder catalog) { }
    }
}
