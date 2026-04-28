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

        DashboardCreatedEvent created = dashboard.DomainEvents.OfType<DashboardCreatedEvent>().Single();
        created.DashboardId.ShouldBe(id);
        created.TenantId.ShouldBe(tenantId);
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
