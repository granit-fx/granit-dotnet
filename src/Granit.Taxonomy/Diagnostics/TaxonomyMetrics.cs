using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Taxonomy.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the Granit.Taxonomy module.
/// Meter: <c>Granit.Taxonomy</c>.
/// </summary>
/// <remarks>
/// Counters cover the Tag lifecycle (<c>granit.taxonomy.tag.created</c> /
/// <c>...deleted</c>) and the assignment lifecycle
/// (<c>granit.taxonomy.assignment.created</c> / <c>...deleted</c>) introduced by
/// T2.2 alongside the polymorphic <c>TagAssignment</c> table.
/// </remarks>
public sealed class TaxonomyMetrics
{
    /// <summary>Meter name — <c>Granit.Taxonomy</c>.</summary>
    public const string MeterName = "Granit.Taxonomy";

    private const string TagTenantId = "tenant_id";
    private const string TagScope = "scope";
    private const string TagTargetType = "target_type";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _tagsCreated;
    private readonly Counter<long> _tagsDeleted;
    private readonly Counter<long> _assignmentsCreated;
    private readonly Counter<long> _assignmentsDeleted;

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

        _assignmentsCreated = meter.CreateCounter<long>(
            "granit.taxonomy.assignment.created",
            description: "Number of tag assignments created (excludes idempotent no-ops on already-assigned rows).");

        _assignmentsDeleted = meter.CreateCounter<long>(
            "granit.taxonomy.assignment.deleted",
            description: "Number of tag assignments removed (includes orphan-cleanup deletions).");
    }

    /// <summary>Records a tag-creation event in <paramref name="scope"/> for <paramref name="tenantId"/>.</summary>
    public void RecordTagCreated(string? tenantId, string scope) =>
        _tagsCreated.Add(1, CreateTagTags(tenantId, scope));

    /// <summary>Records a tag-deletion event.</summary>
    public void RecordTagDeleted(string? tenantId, string scope) =>
        _tagsDeleted.Add(1, CreateTagTags(tenantId, scope));

    /// <summary>Records a new tag-assignment row creation.</summary>
    public void RecordAssignmentCreated(string? tenantId, string targetType) =>
        _assignmentsCreated.Add(1, CreateAssignmentTags(tenantId, targetType));

    /// <summary>Records a tag-assignment row deletion.</summary>
    public void RecordAssignmentDeleted(string? tenantId, string targetType) =>
        _assignmentsDeleted.Add(1, CreateAssignmentTags(tenantId, targetType));

    private static TagList CreateTagTags(string? tenantId, string scope) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
        { TagScope, scope },
    };

    private static TagList CreateAssignmentTags(string? tenantId, string targetType) => new()
    {
        { TagTenantId, tenantId ?? DefaultTenant },
        { TagTargetType, targetType },
    };
}
