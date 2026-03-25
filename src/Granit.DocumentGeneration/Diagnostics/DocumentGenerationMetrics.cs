using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.DocumentGeneration.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the document generation module.
/// Meter: <c>Granit.DocumentGeneration</c>.
/// </summary>
public sealed class DocumentGenerationMetrics
{
    public const string MeterName = "Granit.DocumentGeneration";

    private readonly Counter<long> _documentsGenerated;
    private readonly Counter<long> _generationsFailed;
    private readonly Histogram<double> _generationDuration;

    public DocumentGenerationMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _documentsGenerated = meter.CreateCounter<long>(
            "granit.document_generation.document.generated",
            description: "Number of documents successfully generated.");

        _generationsFailed = meter.CreateCounter<long>(
            "granit.document_generation.document.failed",
            description: "Number of document generation failures.");

        _generationDuration = meter.CreateHistogram<double>(
            "granit.document_generation.document.duration",
            unit: "s",
            description: "Duration of document generation in seconds.");
    }

    public void RecordDocumentGenerated(string? tenantId, string templateType, string format) =>
        _documentsGenerated.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "template_type", templateType },
            { "format", format },
        });

    public void RecordGenerationFailed(string? tenantId, string templateType, string format) =>
        _generationsFailed.Add(1, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "template_type", templateType },
            { "format", format },
        });

    public void RecordGenerationDuration(string? tenantId, string templateType, string format, TimeSpan duration) =>
        _generationDuration.Record(duration.TotalSeconds, new TagList
        {
            { "tenant_id", tenantId ?? "global" },
            { "template_type", templateType },
            { "format", format },
        });
}
