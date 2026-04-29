using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Rendering;
using Granit.Dashboards.Rendering;
using Granit.Dashboards.Widgets;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Rendering;

public sealed class TextWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public TextWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsText()
    {
        new TextWidgetInstanceRenderer(_clock).WidgetType.ShouldBe("Text");
    }

    [Theory]
    [InlineData("Body", TextStyle.Body)]
    [InlineData("Heading", TextStyle.Heading)]
    [InlineData("Subheading", TextStyle.Subheading)]
    [InlineData("Caption", TextStyle.Caption)]
    public async Task RenderAsync_RoundsTripStyleAcrossEveryEnumMember(string persistedStyle, TextStyle expected)
    {
        // The mapper writes Style as a PascalCase string ("Heading"), so the
        // renderer must accept that exact form for every enum member shipped.
        WidgetInstance widget = BuildWidget(
            "Text",
            $"{{\"contentLocalizationKey\":\"Widget:Title\",\"style\":\"{persistedStyle}\"}}");

        WidgetSnapshotEnvelope envelope = await new TextWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.Snapshot.ShouldNotBeNull();
        envelope.Snapshot!.Value.GetProperty("contentLocalizationKey").GetString()
            .ShouldBe("Widget:Title");
        envelope.Snapshot.Value.GetProperty("style").GetString()
            .ShouldBe(expected.ToString());
    }

    [Fact]
    public async Task RenderAsync_SerialisesStyleAsPascalCaseString()
    {
        // ADR-039 §6.1 — enum members travel as PascalCase strings on the wire.
        WidgetInstance widget = BuildWidget(
            "Text",
            "{\"contentLocalizationKey\":\"Widget:Title\",\"style\":\"Heading\"}");

        WidgetSnapshotEnvelope envelope = await new TextWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"style\":\"Heading\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        // Guard against an accidental int-encoded payload (would break the frontend's union).
        raw.Contains("\"style\":1", StringComparison.Ordinal).ShouldBeFalse(raw);
    }

    private static WidgetInstance BuildWidget(string widgetType, string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: widgetType,
            position: 0,
            width: 1,
            height: 1,
            titleLocalizationKey: "Widget:Test",
            configJson: configJson);

    private static WidgetRenderContext BuildContext() =>
        new(
            TenantId: null,
            User: new ClaimsPrincipal(new ClaimsIdentity()),
            Period: null,
            Locale: "en",
            DashboardFilters: new Dictionary<string, string>(),
            ResolvedEntityAliases: new Dictionary<string, EntityAliasBinding>());
}
