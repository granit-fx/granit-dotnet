using System.Security.Claims;
using Granit.Analytics;
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
/// Unit tests for <see cref="PivotWidgetInstanceRenderer"/> — substitute the
/// <see cref="PivotService"/>'s underlying <see cref="IPivotRunner"/> so the
/// renderer's envelope shaping is exercised against a known result. Runner
/// projection logic is covered separately by
/// <see cref="Internal.PivotRunnerTests"/>.
/// </summary>
public sealed class PivotWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public PivotWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsPivot()
    {
        new PivotWidgetInstanceRenderer(new PivotService([]), _clock).WidgetType.ShouldBe("Pivot");
    }

    [Fact]
    public async Task RenderAsync_UnknownQueryName_ReturnsUnavailable()
    {
        PivotWidgetInstanceRenderer renderer = new(new PivotService([]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.MissingQuery",
                "{\"rowFields\":[\"Status\"],\"columnFields\":[\"Region\"],\"valueField\":\"Amount\",\"valueAggregation\":\"Sum\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Pivot");
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryNotFound");
    }

    [Fact]
    public async Task RenderAsync_HappyPath_BuildsPivotWidgetSnapshot()
    {
        StubRunner runner = new(
            "Granit.Test.Items",
            cells:
            [
                new PivotRunnerCell(["Open"], ["EU"], 15m),
                new PivotRunnerCell(["Open"], ["US"], 20m),
                new PivotRunnerCell(["Paid"], ["EU"], 30m),
            ]);

        PivotWidgetInstanceRenderer renderer = new(new PivotService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"rowFields\":[\"Status\"],\"columnFields\":[\"Region\"],\"valueField\":\"Amount\",\"valueAggregation\":\"Sum\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Pivot");
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        envelope.EmittedAt.ShouldBe(Now);
        envelope.Sequence.ShouldBe(1);

        envelope.Snapshot.ShouldNotBeNull();
        System.Text.Json.JsonElement snap = envelope.Snapshot!.Value;
        snap.GetProperty("aggregation").GetString().ShouldBe("Sum");
        snap.GetProperty("valueField").GetString().ShouldBe("Amount");
        snap.GetProperty("rowFields")[0].GetString().ShouldBe("Status");
        snap.GetProperty("columnFields")[0].GetString().ShouldBe("Region");
        snap.GetProperty("cells").GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task RenderAsync_PassesConfigOnToRunner()
    {
        StubRunner runner = new("Granit.Test.Items", cells: []);
        PivotWidgetInstanceRenderer renderer = new(new PivotService([runner]), _clock);

        await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"rowFields\":[\"Status\",\"Region\"],\"columnFields\":[\"Year\"],\"valueField\":\"Amount\",\"valueAggregation\":\"Avg\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        runner.LastRowFields.ShouldBe(["Status", "Region"]);
        runner.LastColumnFields.ShouldBe(["Year"]);
        runner.LastValueField.ShouldBe("Amount");
        runner.LastAggregation.ShouldBe(AggregateFunction.Avg);
    }

    [Fact]
    public async Task RenderAsync_NullCellValue_SerialisesAsJsonNull()
    {
        StubRunner runner = new(
            "Granit.Test.Items",
            cells: [new PivotRunnerCell(["Open"], ["EU"], null)]);

        PivotWidgetInstanceRenderer renderer = new(new PivotService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"rowFields\":[\"Status\"],\"columnFields\":[\"Region\"],\"valueField\":\"Amount\",\"valueAggregation\":\"Avg\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        System.Text.Json.JsonElement cell = envelope.Snapshot!.Value.GetProperty("cells")[0];
        cell.GetProperty("value").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task RenderAsync_SnapshotShape_IsCamelCase()
    {
        StubRunner runner = new(
            "Granit.Test.Items",
            cells: [new PivotRunnerCell(["Open"], ["EU"], 15m)]);

        PivotWidgetInstanceRenderer renderer = new(new PivotService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"rowFields\":[\"Status\"],\"columnFields\":[\"Region\"],\"valueField\":\"Amount\",\"valueAggregation\":\"Sum\"}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"rowFields\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"columnFields\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"rowKeys\":[\"Open\"]", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"columnKeys\":[\"EU\"]", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"RowFields\"", StringComparison.Ordinal).ShouldBeFalse(raw);
    }

    private static WidgetInstance BuildWidget(string queryName, string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Pivot",
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

    private sealed class StubRunner(string name, IReadOnlyList<PivotRunnerCell> cells) : IPivotRunner
    {
        public string Name { get; } = name;

        public IReadOnlyList<string>? LastRowFields { get; private set; }
        public IReadOnlyList<string>? LastColumnFields { get; private set; }
        public string? LastValueField { get; private set; }
        public AggregateFunction LastAggregation { get; private set; }

        public Task<PivotRunnerResult> ExecuteAsync(
            IReadOnlyList<string> rowFields,
            IReadOnlyList<string> columnFields,
            string? valueField,
            AggregateFunction aggregation,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            LastRowFields = rowFields;
            LastColumnFields = columnFields;
            LastValueField = valueField;
            LastAggregation = aggregation;
            return Task.FromResult(new PivotRunnerResult(cells));
        }
    }
}
