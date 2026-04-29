using System.Security.Claims;
using System.Text.Json;
using Granit.Analytics;
using Granit.Analytics.EntityFrameworkCore.Internal;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="TableWidgetInstanceRenderer"/> — substitute the
/// <see cref="TableService"/>'s underlying <see cref="ITableRunner"/> so the
/// renderer's envelope shaping is exercised against a known result, with the
/// runner's projection logic separately covered by
/// <see cref="Internal.TableRunnerTests"/>.
/// </summary>
public sealed class TableWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public TableWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsTable()
    {
        new TableWidgetInstanceRenderer(new TableService([]), _clock).WidgetType.ShouldBe("Table");
    }

    [Fact]
    public async Task RenderAsync_UnknownQueryName_ReturnsUnavailable()
    {
        // No runner registered — surface Unavailable with the dedicated reason
        // key so the frontend can render "Query not found" rather than a
        // generic Error.
        TableWidgetInstanceRenderer renderer = new(new TableService([]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget("Granit.Test.MissingQuery", "{\"visibleColumns\":null,\"pageSize\":10}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Table");
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryNotFound");
    }

    [Fact]
    public async Task RenderAsync_HappyPath_BuildsTableWidgetSnapshot()
    {
        ITableRunner runner = StubRunner.WithRows(
            "Granit.Test.Items",
            columns: [new TableRunnerColumn("name", "Column:Name"), new TableRunnerColumn("amount", "Column:Amount")],
            rows: [JsonSerializer.SerializeToElement(new { name = "Alice", amount = 100 })],
            totalRowCount: 42);

        TableWidgetInstanceRenderer renderer = new(new TableService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget("Granit.Test.Items", "{\"visibleColumns\":null,\"pageSize\":5}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Table");
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);
        envelope.EmittedAt.ShouldBe(Now);
        envelope.Sequence.ShouldBe(1);

        envelope.Snapshot.ShouldNotBeNull();
        JsonElement snap = envelope.Snapshot!.Value;
        snap.GetProperty("totalRowCount").GetInt32().ShouldBe(42);
        snap.GetProperty("columns").GetArrayLength().ShouldBe(2);
        snap.GetProperty("rows").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task RenderAsync_HappyPath_PassesConfigPageSizeAndVisibleColumns()
    {
        var runner = StubRunner.Recording("Granit.Test.Items");
        TableWidgetInstanceRenderer renderer = new(new TableService([runner]), _clock);

        await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                "{\"visibleColumns\":[\"Name\",\"Amount\"],\"pageSize\":7}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        runner.LastVisibleColumns.ShouldBe(["Name", "Amount"]);
        runner.LastPageSize.ShouldBe(7);
    }

    [Fact]
    public async Task RenderAsync_OmittedPageSize_DefaultsToTen()
    {
        // Config can omit pageSize when the widget definition didn't carry one
        // (definition's PageSize is int? and serialises as 0 when null).
        var runner = StubRunner.Recording("Granit.Test.Items");
        TableWidgetInstanceRenderer renderer = new(new TableService([runner]), _clock);

        await renderer.RenderAsync(
            BuildWidget("Granit.Test.Items", "{\"visibleColumns\":null,\"pageSize\":0}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        runner.LastPageSize.ShouldBe(10);
    }

    [Fact]
    public async Task RenderAsync_SnapshotShape_IsCamelCase()
    {
        ITableRunner runner = StubRunner.WithRows(
            "Granit.Test.Items",
            columns: [new TableRunnerColumn("name", "Column:Name")],
            rows: [],
            totalRowCount: 0);

        TableWidgetInstanceRenderer renderer = new(new TableService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget("Granit.Test.Items", "{\"visibleColumns\":null,\"pageSize\":10}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"columns\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"rows\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"totalRowCount\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"labelLocalizationKey\":\"Column:Name\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"Columns\"", StringComparison.Ordinal).ShouldBeFalse(raw);
    }

    private static WidgetInstance BuildWidget(string queryName, string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Table",
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

    private sealed class StubRunner(
        string name,
        IReadOnlyList<TableRunnerColumn> columns,
        IReadOnlyList<JsonElement> rows,
        int totalRowCount) : ITableRunner
    {
        public string Name { get; } = name;

        public IReadOnlyList<string>? LastVisibleColumns { get; private set; }
        public int LastPageSize { get; private set; }
        public IReadOnlyDictionary<string, string>? LastDashboardFilters { get; private set; }

        public Task<TableRunnerResult> ExecuteAsync(
            IReadOnlyList<string>? visibleColumns,
            int pageSize,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            LastVisibleColumns = visibleColumns;
            LastPageSize = pageSize;
            LastDashboardFilters = dashboardFilters;
            return Task.FromResult(new TableRunnerResult(columns, rows, totalRowCount));
        }

        public static StubRunner WithRows(
            string name,
            IReadOnlyList<TableRunnerColumn> columns,
            IReadOnlyList<JsonElement> rows,
            int totalRowCount) =>
            new(name, columns, rows, totalRowCount);

        public static StubRunner Recording(string name) =>
            new(name, [], [], 0);
    }
}
