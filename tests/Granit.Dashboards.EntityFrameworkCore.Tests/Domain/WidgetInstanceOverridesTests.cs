using Granit.Dashboards.Domain;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Domain;

/// <summary>
/// Locks the public shape of <see cref="WidgetInstanceConfig"/> + the
/// <see cref="WidgetInstance.ApplyOverrides"/> behaviour. Pure in-memory tests —
/// the persistence round-trip is covered separately by
/// <c>DashboardPersistenceRoundTripTests</c>.
/// </summary>
public sealed class WidgetInstanceOverridesTests
{
    [Fact]
    public void NewWidget_HasNoOverrides()
    {
        WidgetInstance widget = NewWidget();
        widget.Overrides.ShouldBeNull();
    }

    [Fact]
    public void ApplyOverrides_StoresPayload()
    {
        WidgetInstance widget = NewWidget();
        var overrides = new WidgetInstanceConfig(
            TitleLocalizationKeyOverride: "Widget:Custom.Title",
            ColorOverride: "#ff5500",
            UnitOverride: "€",
            DecimalsOverride: 2,
            Thresholds: [new WidgetThreshold(100m, "#cc0000", WidgetThresholdOperator.GreaterThanOrEqual)]);

        widget.ApplyOverrides(overrides);

        widget.Overrides.ShouldBe(overrides);
        widget.Overrides!.Thresholds.ShouldNotBeNull();
        widget.Overrides.Thresholds.Count.ShouldBe(1);
        widget.Overrides.Thresholds[0].Operator.ShouldBe(WidgetThresholdOperator.GreaterThanOrEqual);
    }

    [Fact]
    public void ApplyOverrides_WithNull_ClearsExistingOverrides()
    {
        WidgetInstance widget = NewWidget();
        widget.ApplyOverrides(new WidgetInstanceConfig(ColorOverride: "#000000"));
        widget.Overrides.ShouldNotBeNull();

        widget.ApplyOverrides(null);

        widget.Overrides.ShouldBeNull();
    }

    [Fact]
    public void WidgetThresholdOperator_EnumOrderingIsStable()
    {
        // Wire format stability — JSON serializes enums as integers when no converter
        // is set. The operator semantics MUST not drift across releases.
        ((int)WidgetThresholdOperator.GreaterThanOrEqual).ShouldBe(0);
        ((int)WidgetThresholdOperator.LessThanOrEqual).ShouldBe(1);
        ((int)WidgetThresholdOperator.Equal).ShouldBe(2);
    }

    [Fact]
    public void WidgetInstanceConfig_AllFieldsNullable_RemainsScopedToWhatChanges()
    {
        var empty = new WidgetInstanceConfig();

        empty.TitleLocalizationKeyOverride.ShouldBeNull();
        empty.ColorOverride.ShouldBeNull();
        empty.UnitOverride.ShouldBeNull();
        empty.DecimalsOverride.ShouldBeNull();
        empty.Thresholds.ShouldBeNull();
    }

    private static WidgetInstance NewWidget()
    {
        var dashboard = Dashboard.Create(
            Guid.NewGuid(), "Sample", DashboardCategory.Finance);
        return dashboard.AddWidget(
            Guid.NewGuid(), "Kpi", 0, 3, 1,
            "Widget:Sample.Slug", "{}", metricName: "Sample.Metric");
    }
}
