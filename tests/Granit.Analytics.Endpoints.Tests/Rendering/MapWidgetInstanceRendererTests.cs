using System.Security.Claims;
using System.Text.Json;
using Granit.Analytics;
using Granit.Analytics.Endpoints.Internal;
using Granit.Analytics.Endpoints.Rendering;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Analytics.Endpoints.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="MapWidgetInstanceRenderer"/> — substitute the
/// <see cref="MapService"/>'s underlying <see cref="IMapRunner"/> so the
/// renderer's envelope shaping is exercised against a known result. Runner
/// projection logic is covered separately by
/// <see cref="Internal.MapRunnerTests"/>.
/// </summary>
public sealed class MapWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public MapWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsMap()
    {
        new MapWidgetInstanceRenderer(new MapService([]), _clock).WidgetType.ShouldBe("Map");
    }

    [Fact]
    public async Task RenderAsync_UnknownQueryName_ReturnsUnavailable()
    {
        MapWidgetInstanceRenderer renderer = new(new MapService([]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.MissingQuery",
                @"{""pointSource"":{""kind"":""lat-lng"",""latitudeColumn"":""Latitude"",""longitudeColumn"":""Longitude""},""popupColumns"":null,""defaultZoom"":5,""defaultCenter"":null,""clusterThreshold"":200,""detailRoute"":null,""tileUrlTemplate"":null}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.WidgetType.ShouldBe("Map");
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.QueryNotFound");
    }

    [Fact]
    public async Task RenderAsync_GeographyPointSource_ReturnsUnavailableUntilFollowUp()
    {
        // PostGIS path is deferred — surface a typed Unavailable so the
        // dashboard renders the rest of its widgets normally.
        StubRunner runner = new("Granit.Test.Items", points: []);
        MapWidgetInstanceRenderer renderer = new(new MapService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                @"{""pointSource"":{""kind"":""geography"",""geographyColumn"":""Position""},""popupColumns"":null,""defaultZoom"":5,""defaultCenter"":null,""clusterThreshold"":200,""detailRoute"":null,""tileUrlTemplate"":null}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Unavailable);
        envelope.ReasonLocalizationKey.ShouldBe("Widget:Unavailable.MapGeographyNotImplemented");
    }

    [Fact]
    public async Task RenderAsync_HappyPath_BuildsMapWidgetSnapshot()
    {
        var id = Guid.NewGuid();
        StubRunner runner = new(
            "Granit.Test.Items",
            points:
            [
                new MapRunnerPoint(id, 48.85, 2.35, JsonSerializer.SerializeToElement(new { name = "Paris" })),
                new MapRunnerPoint(null, 51.50, -0.12, null),
            ]);

        MapWidgetInstanceRenderer renderer = new(new MapService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                @"{""pointSource"":{""kind"":""lat-lng"",""latitudeColumn"":""Latitude"",""longitudeColumn"":""Longitude""},""popupColumns"":[""Name""],""defaultZoom"":7,""defaultCenter"":{""latitude"":50.0,""longitude"":1.0},""clusterThreshold"":150,""detailRoute"":""/cities/{id}"",""tileUrlTemplate"":null}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Map");
        envelope.RefreshHint.ShouldBe(RefreshHint.Dynamic);

        envelope.Snapshot.ShouldNotBeNull();
        JsonElement snap = envelope.Snapshot!.Value;
        snap.GetProperty("defaultZoom").GetInt32().ShouldBe(7);
        snap.GetProperty("clusterThreshold").GetInt32().ShouldBe(150);
        snap.GetProperty("detailRoute").GetString().ShouldBe("/cities/{id}");
        snap.GetProperty("defaultCenter").GetProperty("latitude").GetDouble().ShouldBe(50.0);
        snap.GetProperty("defaultCenter").GetProperty("longitude").GetDouble().ShouldBe(1.0);
        snap.GetProperty("points").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task RenderAsync_DefaultCenterNull_SerialisesAsNullOnTheWire()
    {
        StubRunner runner = new("Granit.Test.Items", points: []);
        MapWidgetInstanceRenderer renderer = new(new MapService([runner]), _clock);

        WidgetSnapshotEnvelope envelope = await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                @"{""pointSource"":{""kind"":""lat-lng"",""latitudeColumn"":""Latitude"",""longitudeColumn"":""Longitude""},""popupColumns"":null,""defaultZoom"":5,""defaultCenter"":null,""clusterThreshold"":200,""detailRoute"":null,""tileUrlTemplate"":null}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        envelope.Snapshot!.Value.GetProperty("defaultCenter").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task RenderAsync_PassesPopupColumnsAndCoordinatesToRunner()
    {
        StubRunner runner = new("Granit.Test.Items", points: []);
        MapWidgetInstanceRenderer renderer = new(new MapService([runner]), _clock);

        await renderer.RenderAsync(
            BuildWidget(
                "Granit.Test.Items",
                @"{""pointSource"":{""kind"":""lat-lng"",""latitudeColumn"":""Lat"",""longitudeColumn"":""Lng""},""popupColumns"":[""Name"",""Country""],""defaultZoom"":5,""defaultCenter"":null,""clusterThreshold"":200,""detailRoute"":null,""tileUrlTemplate"":null}"),
            BuildContext(),
            TestContext.Current.CancellationToken);

        runner.LastLatitudeColumn.ShouldBe("Lat");
        runner.LastLongitudeColumn.ShouldBe("Lng");
        runner.LastPopupColumns.ShouldBe(["Name", "Country"]);
    }

    private static WidgetInstance BuildWidget(string queryName, string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Map",
            position: 0,
            width: 6,
            height: 4,
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

    private sealed class StubRunner(string name, IReadOnlyList<MapRunnerPoint> points) : IMapRunner
    {
        public string Name { get; } = name;

        public string? LastLatitudeColumn { get; private set; }
        public string? LastLongitudeColumn { get; private set; }
        public IReadOnlyList<string>? LastPopupColumns { get; private set; }

        public Task<MapRunnerResult> ExecuteAsync(
            string latitudeColumn,
            string longitudeColumn,
            IReadOnlyList<string>? popupColumns,
            IReadOnlyDictionary<string, string>? dashboardFilters,
            CancellationToken cancellationToken)
        {
            LastLatitudeColumn = latitudeColumn;
            LastLongitudeColumn = longitudeColumn;
            LastPopupColumns = popupColumns;
            return Task.FromResult(new MapRunnerResult(points));
        }
    }
}
