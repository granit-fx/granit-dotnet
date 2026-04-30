using Granit.Dashboards.Domain;
using Granit.Dashboards.Domain.Events;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Domain;

/// <summary>
/// Locks the <see cref="Dashboard"/> aggregate's state machine and the domain events
/// raised at each transition. The persistence side does not influence behaviour —
/// these tests run purely in memory.
/// </summary>
public sealed class DashboardStateMachineTests
{
    [Fact]
    public void Create_StartsInDraft_AndRaisesCreatedEvent()
    {
        var id = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var dashboard = Dashboard.Create(
            id,
            "Finance overview",
            DashboardCategory.Finance,
            tenantId: tenantId,
            sourceDefinitionName: "Granit.Invoicing.FinanceOverview");

        dashboard.Status.ShouldBe(DashboardStatus.Draft);
        dashboard.Name.ShouldBe("Finance overview");
        dashboard.TenantId.ShouldBe(tenantId);
        dashboard.SourceDefinitionName.ShouldBe("Granit.Invoicing.FinanceOverview");

        // Default push policy — admin opts widgets in via RefreshHint, never the
        // whole board (ADR-043 §2). Verticals override on the descriptor.
        dashboard.PushPolicy.ShouldBe(DashboardPushPolicy.WhenWidgetsRequest);

        DashboardCreatedEvent created = dashboard.DomainEvents.OfType<DashboardCreatedEvent>().Single();
        created.DashboardId.ShouldBe(id);
        created.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public void Create_CapturesPushPolicy_FromCallerOverride()
    {
        var dashboard = Dashboard.Create(
            id: Guid.NewGuid(),
            name: "Cockpit",
            category: DashboardCategory.Operations,
            sourceDefinitionName: "Sample.Cockpit",
            sourceDefinitionVersion: "1.0.0",
            pushPolicy: DashboardPushPolicy.Force);

        dashboard.PushPolicy.ShouldBe(DashboardPushPolicy.Force);
    }

    [Fact]
    public void Publish_TransitionsDraftToPublished_AndRaisesEvent()
    {
        Dashboard dashboard = NewDraft();
        dashboard.ClearDomainEvents();

        dashboard.Publish();

        dashboard.Status.ShouldBe(DashboardStatus.Published);
        dashboard.DomainEvents.ShouldContain(e => e is DashboardPublishedEvent);
    }

    [Fact]
    public void Publish_FromArchived_Throws()
    {
        Dashboard dashboard = NewDraft();
        dashboard.Archive();

        Should.Throw<InvalidOperationException>(dashboard.Publish);
    }

    [Fact]
    public void Archive_FromPublished_RaisesEvent()
    {
        Dashboard dashboard = NewDraft();
        dashboard.Publish();
        dashboard.ClearDomainEvents();

        dashboard.Archive();

        dashboard.Status.ShouldBe(DashboardStatus.Archived);
        dashboard.DomainEvents.ShouldContain(e => e is DashboardArchivedEvent);
    }

    [Fact]
    public void Restore_TransitionsArchivedToDraft()
    {
        Dashboard dashboard = NewDraft();
        dashboard.Archive();

        dashboard.Restore();

        dashboard.Status.ShouldBe(DashboardStatus.Draft);
    }

    [Fact]
    public void Restore_FromPublished_Throws()
    {
        Dashboard dashboard = NewDraft();
        dashboard.Publish();

        Should.Throw<InvalidOperationException>(dashboard.Restore);
    }

    [Fact]
    public void Rename_RaisesModifiedEvent_OnlyWhenChanged()
    {
        Dashboard dashboard = NewDraft();
        dashboard.ClearDomainEvents();

        dashboard.Rename("Finance overview");
        dashboard.DomainEvents.ShouldBeEmpty();

        dashboard.Rename("Finance recap");
        dashboard.DomainEvents.OfType<DashboardModifiedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void AddWidget_AppendsWidgetAndRaisesModifiedEvent()
    {
        Dashboard dashboard = NewDraft();
        dashboard.ClearDomainEvents();

        WidgetInstance widget = dashboard.AddWidget(
            Guid.NewGuid(),
            widgetType: "Kpi",
            position: 0,
            width: 3,
            height: 1,
            titleLocalizationKey: "Widget:Sample.Finance.UnpaidCount",
            configJson: "{}",
            metricName: "Sample.UnpaidInvoiceCountMetric");

        dashboard.Widgets.ShouldContain(widget);
        dashboard.DomainEvents.OfType<DashboardModifiedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void RemoveWidget_RemovesAndRaisesEvent()
    {
        Dashboard dashboard = NewDraft();
        WidgetInstance widget = dashboard.AddWidget(
            Guid.NewGuid(), "Kpi", 0, 3, 1, "Widget:Sample.X", "{}", metricName: "M");
        dashboard.ClearDomainEvents();

        dashboard.RemoveWidget(widget.Id);

        dashboard.Widgets.ShouldNotContain(widget);
        dashboard.DomainEvents.OfType<DashboardModifiedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateWidget_UpdatesEditableFields_AndRaisesModifiedEvent()
    {
        Dashboard dashboard = NewDraft();
        WidgetInstance widget = dashboard.AddWidget(
            Guid.NewGuid(), "Kpi", position: 0, width: 3, height: 1,
            titleLocalizationKey: "Widget:Sample.Title",
            configJson: "{\"a\":1}",
            metricName: "M",
            requiredPermission: "P.Read");
        dashboard.ClearDomainEvents();

        bool found = dashboard.UpdateWidget(
            widget.Id, position: 4, width: 6, height: 2,
            titleLocalizationKey: "Widget:Sample.Renamed",
            configJson: "{\"a\":2}");

        found.ShouldBeTrue();
        widget.Position.ShouldBe(4);
        widget.Width.ShouldBe(6);
        widget.Height.ShouldBe(2);
        widget.TitleLocalizationKey.ShouldBe("Widget:Sample.Renamed");
        widget.ConfigJson.ShouldBe("{\"a\":2}");
        // Identity-bound fields stay frozen.
        widget.WidgetType.ShouldBe("Kpi");
        widget.MetricName.ShouldBe("M");
        widget.RequiredPermission.ShouldBe("P.Read");
        dashboard.DomainEvents.OfType<DashboardModifiedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void UpdateWidget_UnknownId_ReturnsFalseAndRaisesNoEvent()
    {
        Dashboard dashboard = NewDraft();
        dashboard.AddWidget(Guid.NewGuid(), "Markdown", 0, 12, 1, "Widget:S", "{}");
        dashboard.ClearDomainEvents();

        bool found = dashboard.UpdateWidget(
            Guid.NewGuid(), 1, 6, 1, "Widget:Other", "{}");

        found.ShouldBeFalse();
        dashboard.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void UpdateWidget_RejectsNegativePosition()
    {
        Dashboard dashboard = NewDraft();
        WidgetInstance widget = dashboard.AddWidget(Guid.NewGuid(), "Markdown", 0, 12, 1, "Widget:S", "{}");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            dashboard.UpdateWidget(widget.Id, -1, 12, 1, "Widget:S", "{}"));
    }

    [Fact]
    public void UpdateWidget_RejectsNonPositiveSize()
    {
        Dashboard dashboard = NewDraft();
        WidgetInstance widget = dashboard.AddWidget(Guid.NewGuid(), "Markdown", 0, 12, 1, "Widget:S", "{}");

        Should.Throw<ArgumentOutOfRangeException>(() =>
            dashboard.UpdateWidget(widget.Id, 0, 0, 1, "Widget:S", "{}"));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            dashboard.UpdateWidget(widget.Id, 0, 12, 0, "Widget:S", "{}"));
    }

    [Fact]
    public void UpdateWidget_RejectsBlankTitleOrConfig()
    {
        Dashboard dashboard = NewDraft();
        WidgetInstance widget = dashboard.AddWidget(Guid.NewGuid(), "Markdown", 0, 12, 1, "Widget:S", "{}");

        Should.Throw<ArgumentException>(() =>
            dashboard.UpdateWidget(widget.Id, 0, 12, 1, "  ", "{}"));
        Should.Throw<ArgumentException>(() =>
            dashboard.UpdateWidget(widget.Id, 0, 12, 1, "Widget:S", "  "));
    }

    [Fact]
    public void Create_RejectsEmptyName()
    {
        Should.Throw<ArgumentException>(() =>
            Dashboard.Create(Guid.NewGuid(), string.Empty, DashboardCategory.General));
    }

    [Fact]
    public void Create_RejectsNonPositiveLayout()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            Dashboard.Create(Guid.NewGuid(), "name", DashboardCategory.General, layoutColumns: 0));

        Should.Throw<ArgumentOutOfRangeException>(() =>
            Dashboard.Create(Guid.NewGuid(), "name", DashboardCategory.General, layoutRowHeight: 0));
    }

    private static Dashboard NewDraft() => Dashboard.Create(
        Guid.NewGuid(),
        "Finance overview",
        DashboardCategory.Finance);
}
