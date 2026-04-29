using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Internal;
using Granit.Dashboards.Widgets;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Internal;

/// <summary>
/// Pins the P2.1 multi-view dispatch fallback chain — request <c>ViewName</c> →
/// <c>DefaultView</c> → first declared view → top-level pool. Covers the four
/// renderer paths the endpoint dispatches through:
/// <list type="number">
///   <item>Single-view dashboard (no Views): <c>dashboard.Widgets</c>, <c>ActiveViewName: null</c>.</item>
///   <item>Multi-view dashboard, ViewName matches entry: persisted widgets, view name echoed.</item>
///   <item>Multi-view dashboard, ViewName matches non-entry: materialised widgets with stable ids.</item>
///   <item>Multi-view dashboard, missing source descriptor: persisted fallback.</item>
/// </list>
/// </summary>
public sealed class ActiveViewResolverTests
{
    private const string DefinitionName = "Granit.Test.MultiViewDashboard";

    [Fact]
    public void Resolve_NoDescriptor_FallsBackToPersistedWidgets_NullViewName()
    {
        Dashboard dashboard = NewDashboard();
        WidgetInstance persisted = AddBanner(dashboard, "banner");

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor: null, requestedViewName: "Anything");

        result.Widgets.Count.ShouldBe(1);
        result.Widgets[0].Id.ShouldBe(persisted.Id);
        result.ActiveViewName.ShouldBeNull();
    }

    [Fact]
    public void Resolve_DescriptorWithoutViews_FallsBackToPersistedWidgets()
    {
        Dashboard dashboard = NewDashboard();
        AddBanner(dashboard, "banner");

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(views: null);

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: null);

        result.ActiveViewName.ShouldBeNull();
        result.Widgets.ShouldBe(dashboard.Widgets);
    }

    [Fact]
    public void Resolve_RequestedViewMatchesEntry_RendersPersistedWidgets()
    {
        Dashboard dashboard = NewDashboard();
        WidgetInstance persisted = AddBanner(dashboard, "entry-banner");

        DashboardView entryView = new("Entry",
            [new MarkdownWidgetDefinition("entry-banner", $"Widget:{DefinitionName}.entry-banner.Body", 0)]);
        DashboardView altView = new("Alt",
            [new MarkdownWidgetDefinition("alt-banner", $"Widget:{DefinitionName}.alt-banner.Body", 0)]);

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(
            views: [entryView, altView],
            defaultView: "Entry");

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: "Entry");

        result.ActiveViewName.ShouldBe("Entry");
        result.Widgets.Count.ShouldBe(1);
        result.Widgets[0].Id.ShouldBe(persisted.Id);                                  // persisted, not materialised
    }

    [Fact]
    public void Resolve_RequestedViewIsNonEntry_MaterialisesEphemeralWidgets()
    {
        Dashboard dashboard = NewDashboard();
        AddBanner(dashboard, "entry-banner");

        DashboardView entryView = new("Entry",
            [new MarkdownWidgetDefinition("entry-banner", $"Widget:{DefinitionName}.entry-banner.Body", 0)]);
        DashboardView altView = new("Alt",
            [new MarkdownWidgetDefinition("alt-banner", $"Widget:{DefinitionName}.alt-banner.Body", 0)]);

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(
            views: [entryView, altView],
            defaultView: "Entry");

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: "Alt");

        result.ActiveViewName.ShouldBe("Alt");
        result.Widgets.Count.ShouldBe(1);
        WidgetInstance materialised = result.Widgets[0];
        materialised.WidgetType.ShouldBe("Markdown");
        materialised.TitleLocalizationKey.ShouldBe($"Widget:{DefinitionName}.alt-banner");
        // Materialised id is deterministic and DIFFERENT from any persisted id —
        // it lives only for the render call, but the same call again gives the
        // same id (frontend cache stays warm across re-fetches).
        materialised.Id.ShouldNotBe(dashboard.Widgets[0].Id);

        ResolvedRenderTarget secondCall = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: "Alt");
        secondCall.Widgets[0].Id.ShouldBe(materialised.Id);
    }

    [Fact]
    public void Resolve_RequestedViewMissing_FallsThroughToDefaultThenFirst()
    {
        Dashboard dashboard = NewDashboard();
        AddBanner(dashboard, "entry-banner");

        DashboardView entryView = new("Entry",
            [new MarkdownWidgetDefinition("entry-banner", $"Widget:{DefinitionName}.entry-banner.Body", 0)]);
        DashboardView altView = new("Alt",
            [new MarkdownWidgetDefinition("alt-banner", $"Widget:{DefinitionName}.alt-banner.Body", 0)]);

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(
            views: [entryView, altView],
            defaultView: "Entry");

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: "DoesNotExist");

        result.ActiveViewName.ShouldBe("Entry");                                      // fell back to DefaultView
    }

    [Fact]
    public void Resolve_NullViewName_PicksDefaultView()
    {
        Dashboard dashboard = NewDashboard();
        AddBanner(dashboard, "entry-banner");

        DashboardView a = new("A",
            [new MarkdownWidgetDefinition("a", $"Widget:{DefinitionName}.a.Body", 0)]);
        DashboardView b = new("B",
            [new MarkdownWidgetDefinition("b", $"Widget:{DefinitionName}.b.Body", 0)]);

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(
            views: [a, b],
            defaultView: "B");

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: null);

        result.ActiveViewName.ShouldBe("B");
    }

    [Fact]
    public void Resolve_NullViewNameAndNoDefault_PicksFirstView()
    {
        Dashboard dashboard = NewDashboard();
        AddBanner(dashboard, "entry-banner");

        DashboardView a = new("A",
            [new MarkdownWidgetDefinition("a", $"Widget:{DefinitionName}.a.Body", 0)]);
        DashboardView b = new("B",
            [new MarkdownWidgetDefinition("b", $"Widget:{DefinitionName}.b.Body", 0)]);

        IDashboardDefinitionDescriptor descriptor = StubDescriptor(
            views: [a, b],
            defaultView: null);

        ResolvedRenderTarget result = ActiveViewResolver.Resolve(
            dashboard, descriptor, requestedViewName: null);

        result.ActiveViewName.ShouldBe("A");                                          // first view wins when DefaultView null
    }

    private static Dashboard NewDashboard() => Dashboard.Create(
        id: Guid.NewGuid(),
        name: "MultiViewDashboard",
        category: DashboardCategory.General,
        sourceDefinitionName: DefinitionName,
        sourceDefinitionVersion: "1.0.0");

    private static WidgetInstance AddBanner(Dashboard dashboard, string slug) =>
        dashboard.AddWidget(
            widgetId: Guid.NewGuid(),
            widgetType: "Markdown",
            position: 0,
            width: 12,
            height: 1,
            titleLocalizationKey: $"Widget:{DefinitionName}.{slug}",
            configJson: "{}");

    private static IDashboardDefinitionDescriptor StubDescriptor(
        IReadOnlyList<DashboardView>? views,
        string? defaultView = null)
    {
        IDashboardDefinitionDescriptor descriptor = Substitute.For<IDashboardDefinitionDescriptor>();
        descriptor.Name.Returns(DefinitionName);
        descriptor.Widgets.Returns(Array.Empty<WidgetDefinition>());
        descriptor.Views.Returns(views);
        descriptor.DefaultView.Returns(defaultView);
        return descriptor;
    }
}
