using Granit.Dashboards.Widgets;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape of <see cref="DashboardView"/> (P2.1) — record carrying
/// a name, a widget list, and optional layout / display name overrides. The
/// rename from "DashboardState" to "DashboardView" is enforced by the type's
/// existence (the old name is gone from the API).
/// </summary>
public sealed class DashboardViewTests
{
    [Fact]
    public void Construction_DefaultsLayoutAndLabelToNull()
    {
        DashboardView view = new(
            Name: "list",
            Widgets:
            [
                new MarkdownWidgetDefinition("Heading", "Widget:S.list.Heading", Position: 0),
            ]);

        view.Name.ShouldBe("list");
        view.Widgets.Count.ShouldBe(1);
        view.Layout.ShouldBeNull();
        view.DisplayNameLocalizationKey.ShouldBeNull();
    }

    [Fact]
    public void View_AcceptsLayoutOverrideAndDisplayKey()
    {
        DashboardLayout layout = new(Columns: 16, RowHeight: 64);

        DashboardView view = new(
            "detail",
            [new MarkdownWidgetDefinition("Body", "Widget:S.detail.Body", Position: 0)],
            Layout: layout,
            DisplayNameLocalizationKey: "Dashboard:Sample.View.detail");

        view.Layout.ShouldBe(layout);
        view.DisplayNameLocalizationKey.ShouldBe("Dashboard:Sample.View.detail");
    }

    [Fact]
    public void DottedSubViewName_SupportsHistoryHeatmap()
    {
        // The proposal documents dot-separated names for sub-views (e.g. "history.heatmap").
        // No URL parsing here — just lock that the record accepts the canonical form.
        DashboardView view = new(
            "history.heatmap",
            [new TextWidgetDefinition("T", "Widget:S.h.T", TextStyle.Heading, Position: 0)]);

        view.Name.ShouldBe("history.heatmap");
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        WidgetDefinition[] widgets =
        [
            new MarkdownWidgetDefinition("X", "Widget:X", Position: 0),
        ];

        DashboardView a = new("v", widgets);
        DashboardView b = new("v", widgets);

        a.ShouldBe(b);
    }
}
