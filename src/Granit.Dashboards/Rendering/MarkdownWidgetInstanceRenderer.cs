using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Metrics;
using Granit.Dashboards;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Timing;

namespace Granit.Dashboards.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Markdown"</c> widget kind.
/// Static content tile — does not query the data layer. The renderer parses
/// <see cref="WidgetInstance.ConfigJson"/> into a <see cref="MarkdownWidgetSnapshot"/>
/// and surfaces <see cref="RefreshHint.Static"/> so the frontend can drop the
/// widget out of its refresh loop.
/// </summary>
internal sealed class MarkdownWidgetInstanceRenderer(IClock clock) : IWidgetInstanceRenderer
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IClock _clock = clock;

    public string WidgetType => "Markdown";

    public Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        MarkdownConfig config = JsonSerializer.Deserialize<MarkdownConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Markdown') has empty ConfigJson — content key cannot be resolved.");

        MarkdownWidgetSnapshot snapshot = new(config.ContentLocalizationKey);

        return Task.FromResult(WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: _clock.Now,
            refreshHint: RefreshHint.Static));
    }

    private sealed record MarkdownConfig(string ContentLocalizationKey);
}
