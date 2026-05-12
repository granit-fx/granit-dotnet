using System.Security.Claims;
using Granit.Analytics;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Dashboards.Widgets;
using Granit.Timing;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.Endpoints.Tests.Rendering;

public sealed class ImageWidgetInstanceRendererTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IClock _clock = Substitute.For<IClock>();

    public ImageWidgetInstanceRendererTests() => _clock.Now.Returns(Now);

    [Fact]
    public void WidgetType_IsImage()
    {
        new ImageWidgetInstanceRenderer(_clock).WidgetType.ShouldBe("Image");
    }

    [Theory]
    [InlineData("Contain", ImageFit.Contain)]
    [InlineData("Cover", ImageFit.Cover)]
    [InlineData("Fill", ImageFit.Fill)]
    public async Task RenderAsync_RoundsTripFitAcrossEveryEnumMember(string persistedFit, ImageFit expected)
    {
        WidgetInstance widget = BuildWidget(
            $"{{\"source\":\"blob:logo\",\"altLocalizationKey\":\"Widget:Logo.Alt\",\"fit\":\"{persistedFit}\"}}");

        WidgetSnapshotEnvelope envelope = await new ImageWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        envelope.Status.ShouldBe(WidgetSnapshotStatus.Snapshot);
        envelope.WidgetType.ShouldBe("Image");
        envelope.RefreshHint.ShouldBe(RefreshHint.Static);
        envelope.EmittedAt.ShouldBe(Now);

        envelope.Snapshot.ShouldNotBeNull();
        envelope.Snapshot!.Value.GetProperty("source").GetString().ShouldBe("blob:logo");
        envelope.Snapshot.Value.GetProperty("altLocalizationKey").GetString().ShouldBe("Widget:Logo.Alt");
        envelope.Snapshot.Value.GetProperty("fit").GetString().ShouldBe(expected.ToString());
    }

    [Fact]
    public async Task RenderAsync_SerialisesFitAsPascalCaseString()
    {
        WidgetInstance widget = BuildWidget(
            "{\"source\":\"https://cdn/logo.png\",\"altLocalizationKey\":\"Widget:Logo.Alt\",\"fit\":\"Cover\"}");

        WidgetSnapshotEnvelope envelope = await new ImageWidgetInstanceRenderer(_clock)
            .RenderAsync(widget, BuildContext(), TestContext.Current.CancellationToken);

        string raw = envelope.Snapshot!.Value.GetRawText();
        raw.Contains("\"fit\":\"Cover\"", StringComparison.Ordinal).ShouldBeTrue(raw);
        raw.Contains("\"fit\":1", StringComparison.Ordinal).ShouldBeFalse(raw);
    }

    private static WidgetInstance BuildWidget(string configJson) =>
        WidgetInstance.Create(
            id: Guid.NewGuid(),
            dashboardId: Guid.NewGuid(),
            widgetType: "Image",
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
