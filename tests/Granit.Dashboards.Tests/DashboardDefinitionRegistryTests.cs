using Granit.Dashboards.Extensions;
using Granit.Dashboards.Widgets;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Tests;

public sealed class DashboardDefinitionRegistryTests
{
    [Fact]
    public void AddDashboardDefinition_RegistersDescriptorAndConcreteType()
    {
        ServiceCollection services = new();
        services.AddGranitDashboards();
        services.AddDashboardDefinition<MarkdownOnlyDashboard>();

        ServiceProvider provider = services.BuildServiceProvider();

        MarkdownOnlyDashboard concrete = provider.GetRequiredService<MarkdownOnlyDashboard>();
        IEnumerable<IDashboardDefinitionDescriptor> descriptors =
            provider.GetServices<IDashboardDefinitionDescriptor>();

        descriptors.ShouldContain(concrete);
        descriptors.Single().Name.ShouldBe("Sample.MarkdownOnly");
    }

    [Fact]
    public void GetAll_OrdersByCategoryThenName()
    {
        ServiceCollection services = new();
        services.AddGranitDashboards();
        services.AddDashboardDefinition<OperationsDashboard>(); // Operations (category=2)
        services.AddDashboardDefinition<MarkdownOnlyDashboard>(); // General (category=0)
        services.AddDashboardDefinition<AlphaGeneralDashboard>(); // General (category=0)

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        IReadOnlyList<IDashboardDefinitionDescriptor> ordered = registry.GetAll();

        ordered.Select(d => d.Name).ShouldBe([
            "Sample.AlphaGeneral",
            "Sample.MarkdownOnly",
            "Sample.Operations",
        ]);
    }

    [Fact]
    public void Find_ReturnsDescriptor_WhenRegistered()
    {
        ServiceCollection services = new();
        services.AddGranitDashboards();
        services.AddDashboardDefinition<MarkdownOnlyDashboard>();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        IDashboardDefinitionDescriptor? found = registry.Find("Sample.MarkdownOnly");

        found.ShouldNotBeNull();
        found.Name.ShouldBe("Sample.MarkdownOnly");
    }

    [Fact]
    public void Find_ReturnsNull_WhenNotRegistered()
    {
        ServiceCollection services = new();
        services.AddGranitDashboards();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        registry.Find("Sample.Missing").ShouldBeNull();
    }

    [Fact]
    public void Find_Throws_OnNullOrEmptyName()
    {
        ServiceCollection services = new();
        services.AddGranitDashboards();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        Should.Throw<ArgumentException>(() => registry.Find(string.Empty));
        Should.Throw<ArgumentException>(() => registry.Find(null!));
    }

    private sealed class MarkdownOnlyDashboard : DashboardDefinition
    {
        public override string Name => "Sample.MarkdownOnly";
        public override DashboardCategory Category => DashboardCategory.General;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new MarkdownWidgetDefinition("Banner", "Widget:Sample.MarkdownOnly.Banner", Position: 0),
        ];
    }

    private sealed class AlphaGeneralDashboard : DashboardDefinition
    {
        public override string Name => "Sample.AlphaGeneral";
        public override DashboardCategory Category => DashboardCategory.General;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new TextWidgetDefinition("Title", "Widget:Sample.AlphaGeneral.Title", TextStyle.Heading, Position: 0),
        ];
    }

    private sealed class OperationsDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Operations";
        public override DashboardCategory Category => DashboardCategory.Operations;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new ImageWidgetDefinition(
                "Logo",
                Source: "https://cdn.example.com/logo.png",
                AltLocalizationKey: "Widget:Sample.Operations.Logo.Alt",
                Position: 0),
        ];
    }
}
