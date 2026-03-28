using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Templating.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the templating module.
/// Meter: <c>Granit.Templating</c>.
/// </summary>
public sealed class TemplatingMetrics
{
    public const string MeterName = "Granit.Templating";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";
    private const string TemplateNameTag = "template_name";

    private readonly Counter<long> _rendersCompleted;
    private readonly Counter<long> _rendersFailed;
    private readonly Histogram<double> _renderDuration;

    public TemplatingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _rendersCompleted = meter.CreateCounter<long>(
            "granit.templating.renders.completed",
            description: "Number of template renders completed successfully.");

        _rendersFailed = meter.CreateCounter<long>(
            "granit.templating.renders.failed",
            description: "Number of template renders that failed.");

        _renderDuration = meter.CreateHistogram<double>(
            "granit.templating.render.duration",
            unit: "s",
            description: "Duration of template rendering in seconds.");
    }

    public void RecordRenderCompleted(string? tenantId, string templateName, TimeSpan duration)
    {
        _rendersCompleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

        _renderDuration.Record(duration.TotalSeconds, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });
    }

    public void RecordRenderFailed(string? tenantId, string templateName) =>
        _rendersFailed.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });
}
