using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Analytics.Metrics;
using Granit.Dashboards.Domain;
using Granit.Dashboards.Rendering;
using Granit.Dashboards.Widgets;
using Granit.Timing;

namespace Granit.Dashboards.Endpoints.Rendering;

/// <summary>
/// <see cref="IWidgetInstanceRenderer"/> for the <c>"Text"</c> widget kind —
/// plain-text tile (heading, subheading, caption) without markdown rendering.
/// Static; does not touch the data layer.
/// </summary>
internal sealed class TextWidgetInstanceRenderer(IClock clock) : IWidgetInstanceRenderer
{
    // ConfigJson was written by WidgetDefinitionToInstanceMapper with
    // PropertyNamingPolicy = CamelCase + Style as a PascalCase string —
    // JsonStringEnumConverter accepts both string and integer enum values.
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

    public string WidgetType => "Text";

    public Task<WidgetSnapshotEnvelope> RenderAsync(
        WidgetInstance widget,
        WidgetRenderContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        TextConfig config = JsonSerializer.Deserialize<TextConfig>(widget.ConfigJson, ConfigJsonOptions)
            ?? throw new InvalidOperationException(
                $"Widget {widget.Id} ('Text') has empty ConfigJson — content key cannot be resolved.");

        TextWidgetSnapshot snapshot = new(config.ContentLocalizationKey, config.Style);

        return Task.FromResult(WidgetSnapshotEnvelope.ForSnapshot(
            widgetType: WidgetType,
            snapshot: JsonSerializer.SerializeToElement(snapshot, SnapshotJsonOptions),
            sequence: 1,
            emittedAt: _clock.Now,
            refreshHint: RefreshHint.Static));
    }

    private sealed record TextConfig(string ContentLocalizationKey, TextStyle Style);
}
