using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Dashboards.Widgets;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.QueryEngine.Filtering;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="ChartWidgetInstanceRenderer"/> — substitute the
/// <see cref="ChartService"/>'s underlying <see cref="IChartRunner"/> so the
/// renderer's envelope shaping is exercised against a known result. Runner
/// projection logic is covered separately by
/// <see cref="Internal.ChartRunnerTests"/>.
/// </summary>
public sealed class ChartWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public ChartWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsChart()
    {
        new ChartWidgetInstanceRenderer(new ChartService([]), _clock).WidgetType.ShouldBe("Chart");
    }

    [Fact]
    public async Task RenderAsync_UnknownQueryName_ReturnsUnavailable()
    {
        ChartWidgetInstanceRenderer renderer = new(new ChartService([]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.MissingQuery",
                "{\"groupBy\":\"Status\",\"aggregation\":\"Count\",\"field\":null,\"chartType\":\"Bar\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Chart");
        envelope.UnavailableReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryNotFound");
    }

    [Fact]
    public async Task RenderAsync_HappyPath_BuildsChartWidgetSnapshot()
    {
        StubRunner runner = new(
            "Granit.Test.Items",
            buckets: [new ChartRunnerBucket("Open", 12m), new ChartRunnerBucket("Paid", 30m)]);

        ChartWidgetInstanceRenderer renderer = new(new ChartService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"groupBy\":\"Status\",\"aggregation\":\"Count\",\"field\":null,\"chartType\":\"Bar\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Chart");
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        envelope.EmittedAt.ShouldBe(Now);
        envelope.Sequence.ShouldBe(1);

        envelope.Snapshot.ShouldNotBeNull();
        System.Text.Json.JsonElement snap = envelope.Snapshot!.Value;
        snap.GetProperty("chartType").GetString().ShouldBe("Bar");
        snap.GetProperty("groupBy").GetString().ShouldBe("Status");
        snap.GetProperty("aggregation").GetString().ShouldBe("Count");
        snap.GetProperty("buckets").GetArrayLength().ShouldBe(2);
    }

    [Theory]
    [InlineData(AggregateFunction.Sum, "12")]
    [InlineData(AggregateFunction.Avg, "12")]
    [InlineData(AggregateFunction.Min, "12")]
    [InlineData(AggregateFunction.Max, "12")]
    public async Task RenderAsync_NumericAggregation_BuildsSnapshotWithDecimalValue(
        AggregateFunction aggregation, string expectedValue)
    {
        // Sum/Avg/Min/Max all flow through the runner to the snapshot now —
        // no special "OperationNotImplemented" branch (B3-5bis closes that gap).
        StubRunner runner = new(
            "Granit.Test.Items",
            buckets: [new ChartRunnerBucket("Open", decimal.Parse(expectedValue, System.Globalization.CultureInfo.InvariantCulture))]);

        ChartWidgetInstanceRenderer renderer = new(new ChartService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                $"{{\"groupBy\":\"Status\",\"aggregation\":\"{aggregation}\",\"field\":\"Amount\",\"chartType\":\"Bar\"}}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        System.Text.Json.JsonElement bucket = envelope.Snapshot!.Value.GetProperty("buckets")[0];
        bucket.GetProperty("label").GetString().ShouldBe("Open");
        bucket.GetProperty("value").GetDecimal().ShouldBe(12m);
    }

    [Fact]
    public async Task RenderAsync_NullBucketValue_SerialisesAsJsonNull()
    {
        // Avg/Min/Max over an empty group surfaces null on the bucket — wire
        // representation is JSON null, not an omitted field.
        StubRunner runner = new(
            "Granit.Test.Items",
            buckets: [new ChartRunnerBucket("Open", null)]);

        ChartWidgetInstanceRenderer renderer = new(new ChartService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"groupBy\":\"Status\",\"aggregation\":\"Avg\",\"field\":\"Amount\",\"chartType\":\"Bar\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        System.Text.Json.JsonElement bucket = envelope.Snapshot!.Value.GetProperty("buckets")[0];
        bucket.GetProperty("value").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Theory]
    [InlineData("Bar")]
    [InlineData("HorizontalBar")]
    [InlineData("Line")]
    [InlineData("Area")]
    [InlineData("Pie")]
    [InlineData("Donut")]
    public async Task RenderAsync_EveryChartType_RoundTripsAsPascalCase(string chartType)
    {
        // ADR-039 §6.1 — enum members on the wire stay PascalCase.
        StubRunner runner = new("Granit.Test.Items", buckets: []);
        ChartWidgetInstanceRenderer renderer = new(new ChartService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                $"{{\"groupBy\":\"Status\",\"aggregation\":\"Count\",\"field\":null,\"chartType\":\"{chartType}\"}}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains($"\"chartType\":\"{chartType}\"", StringComparison.Ordinal).ShouldBeTrue(raw);
    }

    [Fact]
    public async Task RenderAsync_PassesConfigOnToRunner()
    {
        StubRunner runner = new("Granit.Test.Items", buckets: []);
        ChartWidgetInstanceRenderer renderer = new(new ChartService([runner]), _clock);

        await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"groupBy\":\"Status\",\"aggregation\":\"Count\",\"field\":null,\"chartType\":\"Pie\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        runner.LastGroupBy.ShouldBe("Status");
        runner.LastAggregation.ShouldBe(AggregateFunction.Count);
        runner.LastField.ShouldBeNull();
    }

    private static WidgetInstance BuildWidget(string queryName, string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Chart",
            position: 0,
            width: 6,
            height: 3,
            titleLocalizationKey: "Widget:Test",
            configJson: configJson,
            queryName: queryName);

    private static WidgetRenderContext BuildContext() =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: null,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());

    private sealed class StubRunner(string name, IReadOnlyList<ChartRunnerBucket> buckets) : IChartRunner
    {
        public string Name { get; } = name;

        public string? LastGroupBy { get; private set; }
        public AggregateFunction LastAggregation { get; private set; }
        public string? LastField { get; private set; }
        public IReadOnlyDictionary<string, string>? LastDashboardFilters { get; private set; }

        public Task<ChartRunnerResult> ExecuteAsync(
            string groupBy,
            AggregateFunction aggregation,
            string? field,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            LastGroupBy = groupBy;
            LastAggregation = aggregation;
            LastField = field;
            LastDashboardFilters = dashboardFilters;
            return Task.FromResult(new ChartRunnerResult(buckets));
        }
    }
}
