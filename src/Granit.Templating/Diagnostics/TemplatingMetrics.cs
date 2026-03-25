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

    private readonly Counter<long> _draftsCreated;
    private readonly Counter<long> _draftsUpdated;
    private readonly Counter<long> _draftsDeleted;
    private readonly Counter<long> _templatesPublished;
    private readonly Counter<long> _templatesUnpublished;
    private readonly Counter<long> _rendersCompleted;
    private readonly Counter<long> _rendersFailed;
    private readonly Histogram<double> _renderDuration;

    public TemplatingMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _draftsCreated = meter.CreateCounter<long>(
            "granit.templating.drafts.created",
            description: "Number of template drafts created.");

        _draftsUpdated = meter.CreateCounter<long>(
            "granit.templating.drafts.updated",
            description: "Number of template drafts updated.");

        _draftsDeleted = meter.CreateCounter<long>(
            "granit.templating.drafts.deleted",
            description: "Number of template drafts deleted.");

        _templatesPublished = meter.CreateCounter<long>(
            "granit.templating.templates.published",
            description: "Number of templates published.");

        _templatesUnpublished = meter.CreateCounter<long>(
            "granit.templating.templates.unpublished",
            description: "Number of templates unpublished (archived).");

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

    public void RecordDraftCreated(string? tenantId, string templateName) =>
        _draftsCreated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

    public void RecordDraftUpdated(string? tenantId, string templateName) =>
        _draftsUpdated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

    public void RecordDraftDeleted(string? tenantId, string templateName) =>
        _draftsDeleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

    public void RecordPublished(string? tenantId, string templateName) =>
        _templatesPublished.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

    public void RecordUnpublished(string? tenantId, string templateName) =>
        _templatesUnpublished.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { TemplateNameTag, templateName },
        });

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
