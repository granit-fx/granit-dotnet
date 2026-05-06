using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Taxonomy.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit.Taxonomy module.
/// Meter: <c>Granit.Taxonomy</c>.
/// </summary>
/// <remarks>
/// T1.3 baseline counters cover the tag lifecycle. Assignment counters
/// (<c>granit.taxonomy.assignment.created</c> / <c>...assignment.deleted</c>) are
/// added in T2.2 alongside the polymorphic <c>TagAssignment</c> table and the
/// <c>ITagAssignmentService</c>.
/// </remarks>
public sealed class TaxonomyMetrics
{
    /// <summary>Meter name — <c>Granit.Taxonomy</c>.</summary>
    public const string MeterName = "Granit.Taxonomy";

    private const string TagTenantId = "tenant_id";
    private const string TagScope = "scope";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _tagsCreated;
    private readonly Counter<long> _tagsDeleted;

    /// <summary>Initialises the meter and counters.</summary>
    public TaxonomyMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _tagsCreated = meter.CreateCounter<long>(
            "granit.taxonomy.tag.created",
            description: "Number of tags created.");

        _tagsDeleted = meter.CreateCounter<long>(
            "granit.taxonomy.tag.deleted",
            description: "Number of tags deleted.");
    }

    /// <summary>Records a tag-creation event in <paramref name="scope"/> for <paramref name="tenantId"/>.</summary>
    public void RecordTagCreated(string? tenantId, string scope) =>
        _tagsCreated.Add(1, CreateTags(tenantId, scope));

    /// <summary>Records a tag-deletion event.</summary>
    public void RecordTagDeleted(string? tenantId, string scope) =>
        _tagsDeleted.Add(1, CreateTags(tenantId, scope));

    private static TagList CreateTags(string? tenantId, string scope) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
        { TagScope, scope },
    };
}
