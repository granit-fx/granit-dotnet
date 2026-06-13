using Granit.AI.Tools.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Granit.AI.Tools.Tests;

public sealed class AIToolsServiceCollectionExtensionsTests
{
    [Fact]
    public void Opt_in_registration_exposes_the_tool_through_the_registry()
    {
        ServiceCollection services = new();
        services.AddGranitAITools(tools => tools.Add<FakeAITool>());

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IAIToolRegistry registry = scope.ServiceProvider.GetRequiredService<IAIToolRegistry>();

        registry.Tools.ShouldHaveSingleItem().ShouldBeOfType<FakeAITool>();
    }

    [Fact]
    public void A_tool_is_absent_unless_registered()
    {
        ServiceCollection services = new();
        services.AddGranitAITools();

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IAIToolRegistry registry = scope.ServiceProvider.GetRequiredService<IAIToolRegistry>();

        registry.Tools.ShouldBeEmpty();
        registry.TryGet("fake_tool", out _).ShouldBeFalse();
    }

    [Fact]
    public void Instance_registration_is_returned_by_the_registry()
    {
        FakeAITool instance = new(name: "preset");
        ServiceCollection services = new();
        services.AddGranitAITools(tools => tools.Add(instance));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IAIToolRegistry registry = scope.ServiceProvider.GetRequiredService<IAIToolRegistry>();

        registry.TryGet("preset", out IAITool? resolved).ShouldBeTrue();
        resolved.ShouldBeSameAs(instance);
    }

    [Fact]
    public void Projector_resolves_from_the_registry_through_DI()
    {
        ServiceCollection services = new();
        services.AddGranitAITools(tools => tools.Add<FakeAITool>());

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();

        IAIToolProjector projector = scope.ServiceProvider.GetRequiredService<IAIToolProjector>();

        projector.ProjectAll().ShouldHaveSingleItem().Name.ShouldBe("fake_tool");
    }
}
