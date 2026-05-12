using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Dashboards.Widgets;
using Granit.Timing;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Image"</c> widget kind —
/// static image tile (logo, illustration, banner). The renderer surfaces the
/// declarative source / alt key / fit mode unchanged; the frontend resolves
/// blob references and the alt-text localization key against the active
/// session and culture.
/// </summary>
internal sealed class ImageWidgetInstanceRenderer(IClock clock) : IWidgetInstanceRenderer
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IClock _clock = clock;

    public string WidgetType => "Image";

    public Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        ImageConfig config = JsonSerializer.Deserialize<ImageConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Image') has empty ConfigJson — image source cannot be resolved.");

        ImageWidgetSnapshot snapshot = new(config.Source, config.AltLocalizationKey, config.Fit);

        return Task.FromResult(WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: _clock.Now,
            refreshHint: RefreshHint.Static));
    }

    private sealed record ImageConfig(string Source, string AltLocalizationKey, ImageFit Fit);
}
