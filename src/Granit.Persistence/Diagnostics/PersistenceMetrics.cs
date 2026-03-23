using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Persistence.Diagnostics;

/// <summary>
/// Metrics for Granit.Persistence EF Core interceptors and data operations.
/// </summary>
/// <remarks>
/// Meter name: <c>Granit.Persistence</c>. All metric names follow the
/// <c>granit.persistence.{entity}.{action}</c> convention.
/// </remarks>
public sealed class PersistenceMetrics
{
    public const string MeterName = "Granit.Persistence";

    private const string TagTenantId = "tenant_id";
    private const string DefaultTenant = "global";

    private readonly Counter<long> _entitiesAudited;
    private readonly Counter<long> _entitiesSoftDeleted;
    private readonly Counter<long> _concurrencyStampsGenerated;
    private readonly Counter<long> _domainEventsDispatched;
    private readonly Counter<long> _integrationEventsDispatched;
    private readonly Counter<long> _entitiesPurged;

    public PersistenceMetrics(IMeterFactory meterFactory)
    {
        Meter meter = meterFactory.Create(MeterName);

        _entitiesAudited = meter.CreateCounter<long>(
            "granit.persistence.entity.audited",
            description: "Number of entities processed by the audit interceptor.");

        _entitiesSoftDeleted = meter.CreateCounter<long>(
            "granit.persistence.entity.soft_deleted",
            description: "Number of entities converted from hard delete to soft delete.");

        _concurrencyStampsGenerated = meter.CreateCounter<long>(
            "granit.persistence.concurrency_stamp.generated",
            description: "Number of concurrency stamps generated on save.");

        _domainEventsDispatched = meter.CreateCounter<long>(
            "granit.persistence.domain_event.dispatched",
            description: "Number of domain events dispatched after SaveChanges.");

        _integrationEventsDispatched = meter.CreateCounter<long>(
            "granit.persistence.integration_event.dispatched",
            description: "Number of integration events dispatched after SaveChanges.");

        _entitiesPurged = meter.CreateCounter<long>(
            "granit.persistence.entity.purged",
            description: "Number of soft-deleted entities permanently purged.");
    }

    public void RecordEntityAudited(string? tenantId, string operation) =>
        _entitiesAudited.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "operation", operation },
        });

    public void RecordEntitySoftDeleted(string? tenantId) =>
        _entitiesSoftDeleted.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordConcurrencyStampGenerated(string? tenantId) =>
        _concurrencyStampsGenerated.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });

    public void RecordDomainEventDispatched(string? tenantId, string eventType) =>
        _domainEventsDispatched.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "event_type", eventType },
        });

    public void RecordIntegrationEventDispatched(string? tenantId, string eventType) =>
        _integrationEventsDispatched.Add(1, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
            { "event_type", eventType },
        });

    public void RecordEntitiesPurged(string? tenantId, int count) =>
        _entitiesPurged.Add(count, new TagList
        {
            { TagTenantId, tenantId ?? DefaultTenant },
        });
}
