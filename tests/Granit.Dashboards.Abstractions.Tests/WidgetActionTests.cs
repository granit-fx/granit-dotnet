using Shouldly;
using Xunit;

namespace Granit.Dashboards.Abstractions.Tests;

/// <summary>
/// Locks the public shape of <see cref="WidgetAction"/> + the two enums (P1.5).
/// Records stay POCO — the dispatch itself is frontend logic. These tests pin the
/// wire format so the typescript discriminated unions on <c>granit-front</c> have a
/// stable contract.
/// </summary>
public sealed class WidgetActionTests
{
    [Fact]
    public void Construction_DefaultsParamsToNull()
    {
        WidgetAction action = new(
            WidgetActionTrigger.Click,
            WidgetActionKind.Navigate,
            Target: "/invoicing?status=unpaid");

        action.Trigger.ShouldBe(WidgetActionTrigger.Click);
        action.Kind.ShouldBe(WidgetActionKind.Navigate);
        action.Target.ShouldBe("/invoicing?status=unpaid");
        action.Params.ShouldBeNull();
    }

    [Fact]
    public void Construction_AcceptsParamsDictionary()
    {
        WidgetAction action = new(
            WidgetActionTrigger.RowClick,
            WidgetActionKind.OpenDetail,
            Target: "InvoiceDrawer",
            Params: new Dictionary<string, string>
            {
                ["invoiceId"] = "${row.id}",
                ["mode"] = "readonly",
            });

        action.Params.ShouldNotBeNull();
        action.Params.Count.ShouldBe(2);
        action.Params["invoiceId"].ShouldBe("${row.id}");
        action.Params["mode"].ShouldBe("readonly");
    }

    [Fact]
    public void Trigger_EnumOrderingIsStable()
    {
        // Wire format stability — the four trigger values pin the contract.
        ((int)WidgetActionTrigger.Click).ShouldBe(0);
        ((int)WidgetActionTrigger.RowClick).ShouldBe(1);
        ((int)WidgetActionTrigger.SeriesClick).ShouldBe(2);
        ((int)WidgetActionTrigger.LegendClick).ShouldBe(3);
    }

    [Fact]
    public void Kind_EnumOrderingIsStable()
    {
        ((int)WidgetActionKind.Navigate).ShouldBe(0);
        ((int)WidgetActionKind.OpenDashboardView).ShouldBe(1);
        ((int)WidgetActionKind.OpenDashboard).ShouldBe(2);
        ((int)WidgetActionKind.ExportData).ShouldBe(3);
        ((int)WidgetActionKind.OpenDetail).ShouldBe(4);
    }

    [Fact]
    public void RecordEquality_HoldsByValue()
    {
        WidgetAction a = new(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/x");
        WidgetAction b = new(WidgetActionTrigger.Click, WidgetActionKind.Navigate, "/x");

        a.ShouldBe(b);
    }
}
