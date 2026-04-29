using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Endpoints.Rendering;
using Granit.Dashboards.Rendering;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Rendering;

public sealed class MarkdownWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public MarkdownWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsMarkdown()
    {
        new MarkdownWidgetInstanceRenderer(_clock).WidgetType.ShouldBe("Markdown");
    }

    [Fact]
    public async Task RenderAsync_BuildsSnapshotEnvelopeFromConfigJson()
    {
        WidgetInstance widget = BuildWidget(
            "Markdown",
            "{\"contentLocalizationKey\":\"Widget:Granit.Invoicing.Banner\"}");

        WidgetSnapshotEnvelope envelope = await new MarkdownWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Markdown");
        envelope.RefreshHint.ShouldBe(RefreshHint.Static);
        envelope.EmittedAt.ShouldBe(Now);
        envelope.Sequence.ShouldBe(1);
        envelope.ReasonLocalizationKey.ShouldBeNull();

        envelope.Snapshot.ShouldNotBeNull();
        envelope.Snapshot!.Value
            .GetProperty("contentLocalizationKey").GetString()
            .ShouldBe("Widget:Granit.Invoicing.Banner");
    }

    [Fact]
    public async Task RenderAsync_SnapshotPropertiesAreCamelCase()
    {
        // Locks ADR-039 §6.1 — wire convention is camelCase for properties.
        WidgetInstance widget = BuildWidget(
            "Markdown",
            "{\"contentLocalizationKey\":\"Widget:Test\"}");

        WidgetSnapshotEnvelope envelope = await new MarkdownWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"contentLocalizationKey\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"ContentLocalizationKey\"", StringComparison.Ordinal).ShouldBeFalse(raw);
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
