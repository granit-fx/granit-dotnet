using Granit.Dashboards.Domain;
using Granit.Dashboards.Domain.Events;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Domain;

/// <summary>
/// Locks the contract of <see cref="Dashboard.Resync"/>: widget pool is rebuilt from
/// the descriptor's current entry-view widgets, structural metadata is refreshed,
/// per-instance overrides are carried by <c>Widget:{Name}.{slug}</c> match, and
/// user-renamable / lifecycle fields (<c>Name</c>, <c>Status</c>) are intentionally
/// preserved. Pure in-memory tests — the persistence round-trip is covered by
/// <c>DashboardPersistenceRoundTripTests</c>.
/// </summary>
public sealed class DashboardResyncTests
{
    private const string SourceName = "Granit.Test.SampleDashboard";

    [Fact]
    public void Resync_ReplacesWidgetPool_AndCarriesOverridesBySlug()
    {
        Dashboard dashboard = NewImported("1.0.0");
        WidgetInstance kept = dashboard.AddWidget(
            Guid.NewGuid(), "Kpi", 0, 4, 2, $"Widget:{SourceName}.unpaid-total", "{}", metricName: "Granit.Invoicing.UnpaidInvoiceTotal");
        kept.ApplyOverrides(new WidgetInstanceConfig(ColorOverride: "#ff5500", DecimalsOverride: 2));
        dashboard.AddWidget(
            Guid.NewGuid(), "Kpi", 1, 4, 2, $"Widget:{SourceName}.dropped-slug", "{}", metricName: "Granit.Sample.Stale");

        ResyncWidgetInput[] inputs =
        [
            new ResyncWidgetInput(
                WidgetId: Guid.NewGuid(),
                WidgetType: "Kpi",
                Position: 0,
                Width: 4,
                Height: 2,
                TitleLocalizationKey: $"Widget:{SourceName}.unpaid-total",
                ConfigJson: "{\"refreshed\":true}",
                MetricName: "Granit.Invoicing.UnpaidInvoiceTotal"),
            new ResyncWidgetInput(
                WidgetId: Guid.NewGuid(),
                WidgetType: "Chart",
                Position: 1,
                Width: 6,
                Height: 3,
                TitleLocalizationKey: $"Widget:{SourceName}.new-chart",
                ConfigJson: "{}",
                QueryName: "Granit.Sample.Query"),
        ];

        DashboardResyncSummary summary = dashboard.Resync(
            newSourceDefinitionVersion: "1.1.0",
            newLayoutColumns: 12,
            newLayoutRowHeight: 80,
            newIsSystem: false,
            newPushPolicy: DashboardPushPolicy.WhenWidgetsRequest,
            incomingWidgets: inputs);

        summary.PreviousSourceDefinitionVersion.ShouldBe("1.0.0");
        summary.NewSourceDefinitionVersion.ShouldBe("1.1.0");
        summary.WidgetsAdded.ShouldBe(1);                                    // new-chart
        summary.WidgetsRemoved.ShouldBe(1);                                  // dropped-slug
        summary.OverridesCarriedOver.ShouldBe(1);                            // unpaid-total

        dashboard.SourceDefinitionVersion.ShouldBe("1.1.0");
        dashboard.Widgets.Count.ShouldBe(2);

        WidgetInstance carried = dashboard.Widgets.Single(w => w.TitleLocalizationKey.EndsWith("unpaid-total", StringComparison.Ordinal));
        carried.ConfigJson.ShouldBe("{\"refreshed\":true}");                 // descriptor wins on config
        carried.Overrides.ShouldNotBeNull();
        carried.Overrides!.ColorOverride.ShouldBe("#ff5500");                // override carried
        carried.Overrides.DecimalsOverride.ShouldBe(2);

        WidgetInstance added = dashboard.Widgets.Single(w => w.TitleLocalizationKey.EndsWith("new-chart", StringComparison.Ordinal));
        added.Overrides.ShouldBeNull();                                      // no slug-match → no override
    }

    [Fact]
    public void Resync_PreservesUserRenamableNameAndLifecycleStatus()
    {
        Dashboard dashboard = NewImported("1.0.0");
        dashboard.Rename("My Custom Finance");
        dashboard.AddWidget(Guid.NewGuid(), "Kpi", 0, 4, 2, $"Widget:{SourceName}.kpi", "{}");
        dashboard.Publish();

        dashboard.Resync(
            newSourceDefinitionVersion: "2.0.0",
            newLayoutColumns: 12,
            newLayoutRowHeight: 80,
            newIsSystem: false,
            newPushPolicy: DashboardPushPolicy.WhenWidgetsRequest,
            incomingWidgets:
            [
                new ResyncWidgetInput(
                    Guid.NewGuid(), "Kpi", 0, 4, 2, $"Widget:{SourceName}.kpi", "{}"),
            ]);

        dashboard.Name.ShouldBe("My Custom Finance");                        // not overwritten
        dashboard.Status.ShouldBe(DashboardStatus.Published);                // not reset to Draft
    }

    [Fact]
    public void Resync_RefreshesStructuralMetadata()
    {
        Dashboard dashboard = NewImported("1.0.0");

        dashboard.Resync(
            newSourceDefinitionVersion: "1.5.0",
            newLayoutColumns: 24,
            newLayoutRowHeight: 60,
            newIsSystem: true,
            newPushPolicy: DashboardPushPolicy.Force,
            incomingWidgets: []);

        dashboard.SourceDefinitionVersion.ShouldBe("1.5.0");
        dashboard.LayoutColumns.ShouldBe(24);
        dashboard.LayoutRowHeight.ShouldBe(60);
        dashboard.IsSystem.ShouldBeTrue();
        dashboard.PushPolicy.ShouldBe(DashboardPushPolicy.Force);            // refreshed from descriptor
    }

    [Fact]
    public void Resync_RaisesDashboardResyncedEvent_WithChurnSummary()
    {
        Dashboard dashboard = NewImported("1.0.0");
        dashboard.AddWidget(Guid.NewGuid(), "Kpi", 0, 4, 2, $"Widget:{SourceName}.dropped", "{}");
        // Drain creation events so the assertion targets the resync emission only.
        ((IDomainEventSource)dashboard).DomainEvents.Count.ShouldBeGreaterThan(0);
        ((IDomainEventSource)dashboard).ClearDomainEvents();

        dashboard.Resync(
            newSourceDefinitionVersion: "1.1.0",
            newLayoutColumns: 12,
            newLayoutRowHeight: 80,
            newIsSystem: false,
            newPushPolicy: DashboardPushPolicy.WhenWidgetsRequest,
            incomingWidgets:
            [
                new ResyncWidgetInput(
                    Guid.NewGuid(), "Kpi", 0, 4, 2, $"Widget:{SourceName}.added", "{}"),
            ]);

        DashboardResyncedEvent evt = ((IDomainEventSource)dashboard).DomainEvents
            .OfType<DashboardResyncedEvent>()
            .ShouldHaveSingleItem();
        evt.PreviousSourceDefinitionVersion.ShouldBe("1.0.0");
        evt.NewSourceDefinitionVersion.ShouldBe("1.1.0");
        evt.WidgetsAdded.ShouldBe(1);
        evt.WidgetsRemoved.ShouldBe(1);
        evt.OverridesCarriedOver.ShouldBe(0);
    }

    [Fact]
    public void Resync_OnAdHocDashboard_Throws()
    {
        // No SourceDefinitionName → nothing to resync against.
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(), name: "Adhoc", category: DashboardCategory.General);

        Should.Throw<InvalidOperationException>(() => dashboard.Resync(
            newSourceDefinitionVersion: "1.0.0",
            newLayoutColumns: 12,
            newLayoutRowHeight: 80,
            newIsSystem: false,
            newPushPolicy: DashboardPushPolicy.WhenWidgetsRequest,
            incomingWidgets: []));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resync_RejectsBlankNewVersion(string blank)
    {
        Dashboard dashboard = NewImported("1.0.0");

        Should.Throw<ArgumentException>(() => dashboard.Resync(
            newSourceDefinitionVersion: blank,
            newLayoutColumns: 12,
            newLayoutRowHeight: 80,
            newIsSystem: false,
            newPushPolicy: DashboardPushPolicy.WhenWidgetsRequest,
            incomingWidgets: []));
    }

    private static Dashboard NewImported(string version) => Dashboard.Create(
        id: Guid.NewGuid(),
        name: "SampleDashboard",
        category: DashboardCategory.Finance,
        sourceDefinitionName: SourceName,
        sourceDefinitionVersion: version);
}
