using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the responsive layout shape (P1.4): base layout + per-breakpoint overrides
/// + per-widget size dictionary + ordered slug list + hidden-widgets set. Records
/// stay POCO — the merge logic itself lives in the frontend.
/// </summary>
public sealed class DashboardLayoutTests
{
    [Fact]
    public void Default_ProvidesA12ColumnGrid()
    {
        DashboardLayout layout = DashboardLayout.Default;

        layout.Columns.ShouldBe(12);
        layout.RowHeight.ShouldBe(80);
        layout.WidgetSizes.ShouldBeNull();
        layout.WidgetOrder.ShouldBeNull();
        layout.Breakpoints.ShouldBeNull();
    }

    [Fact]
    public void With_BreakpointOverride_PreservesBaseFields()
    {
        Dictionary<DashboardBreakpoint, DashboardLayoutOverride> breakpoints = new()
        {
            [DashboardBreakpoint.Xs] = new(
                Columns: 4,
                WidgetSizes: new Dictionary<string, WidgetSize>
                {
                    ["UnpaidCount"] = new(4, 1),
                },
                HiddenWidgets: new HashSet<string> { "Banner" }),
        };

        DashboardLayout layout = DashboardLayout.Default with { Breakpoints = breakpoints };

        layout.Columns.ShouldBe(12);
        layout.Breakpoints.ShouldNotBeNull();
        layout.Breakpoints[DashboardBreakpoint.Xs].Columns.ShouldBe(4);
        layout.Breakpoints[DashboardBreakpoint.Xs].HiddenWidgets!.ShouldContain("Banner");
    }

    [Fact]
    public void Override_AllFieldsNullable_KeepsScopedToWhatChanges()
    {
        DashboardLayoutOverride empty = new();

        empty.Columns.ShouldBeNull();
        empty.RowHeight.ShouldBeNull();
        empty.WidgetSizes.ShouldBeNull();
        empty.WidgetOrder.ShouldBeNull();
        empty.HiddenWidgets.ShouldBeNull();
    }

    [Fact]
    public void Breakpoint_EnumOrderingIsStable()
    {
        // The frontend may sort breakpoints by enum value to apply overrides progressively.
        ((int)DashboardBreakpoint.Xs).ShouldBe(0);
        ((int)DashboardBreakpoint.Sm).ShouldBe(1);
        ((int)DashboardBreakpoint.Md).ShouldBe(2);
        ((int)DashboardBreakpoint.Lg).ShouldBe(3);
        ((int)DashboardBreakpoint.Xl).ShouldBe(4);
    }
}
