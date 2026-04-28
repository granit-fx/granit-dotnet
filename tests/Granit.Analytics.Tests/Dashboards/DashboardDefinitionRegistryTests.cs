using Granit.Analytics.Dashboards;
using Granit.Analytics.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Tests.Dashboards;

public sealed class DashboardDefinitionRegistryTests
{
    [Fact]
    public void AddDashboardDefinition_RegistersDescriptorAndConcreteType()
    {
        ServiceCollection services = new();
        services.AddGranitAnalytics();
        services.AddDashboardDefinition<FinanceDashboard>();

        ServiceProvider provider = services.BuildServiceProvider();

        FinanceDashboard concrete = provider.GetRequiredService<FinanceDashboard>();
        IEnumerable<IDashboardDefinitionDescriptor> descriptors =
            provider.GetServices<IDashboardDefinitionDescriptor>();

        descriptors.ShouldContain(concrete);
        descriptors.Single().Name.ShouldBe("Sample.Finance");
    }

    [Fact]
    public void GetAll_OrdersByCategoryThenName()
    {
        ServiceCollection services = new();
        services.AddGranitAnalytics();
        services.AddDashboardDefinition<OperationsDashboard>();   // Operations (category=2)
        services.AddDashboardDefinition<FinanceDashboard>();      // Finance (category=1)
        services.AddDashboardDefinition<AlphaFinanceDashboard>(); // Finance (category=1), Alpha < Finance

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        IReadOnlyList<IDashboardDefinitionDescriptor> ordered = registry.GetAll();

        ordered.Select(d => d.Name).ShouldBe([
            "Sample.AlphaFinance",
            "Sample.Finance",
            "Sample.Operations",
        ]);
    }

    [Fact]
    public void Find_ReturnsDescriptor_WhenRegistered()
    {
        ServiceCollection services = new();
        services.AddGranitAnalytics();
        services.AddDashboardDefinition<FinanceDashboard>();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        IDashboardDefinitionDescriptor? found = registry.Find("Sample.Finance");

        found.ShouldNotBeNull();
        found.Name.ShouldBe("Sample.Finance");
    }

    [Fact]
    public void Find_ReturnsNull_WhenNotRegistered()
    {
        ServiceCollection services = new();
        services.AddGranitAnalytics();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        registry.Find("Sample.Missing").ShouldBeNull();
    }

    [Fact]
    public void Find_Throws_OnNullOrEmptyName()
    {
        ServiceCollection services = new();
        services.AddGranitAnalytics();

        IDashboardDefinitionRegistry registry =
            services.BuildServiceProvider().GetRequiredService<IDashboardDefinitionRegistry>();

        Should.Throw<ArgumentException>(() => registry.Find(string.Empty));
        Should.Throw<ArgumentException>(() => registry.Find(null!));
    }

    private sealed class FinanceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Finance";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new KpiWidgetDefinition("UnpaidCount", "Sample.UnpaidInvoiceCount", Position: 0),
            new KpiWidgetDefinition("UnpaidTotal", "Sample.UnpaidInvoiceTotal", Position: 1),
        ];
    }

    private sealed class AlphaFinanceDashboard : DashboardDefinition
    {
        public override string Name => "Sample.AlphaFinance";
        public override DashboardCategory Category => DashboardCategory.Finance;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new KpiWidgetDefinition("Total", "Sample.UnpaidInvoiceTotal", Position: 0),
        ];
    }

    private sealed class OperationsDashboard : DashboardDefinition
    {
        public override string Name => "Sample.Operations";
        public override DashboardCategory Category => DashboardCategory.Operations;
        public override IReadOnlyList<WidgetDefinition> Widgets { get; } = [
            new MarkdownWidgetDefinition(
                "Banner",
                "Widget:Sample.Operations.Banner",
                Position: 0),
        ];
    }
}
