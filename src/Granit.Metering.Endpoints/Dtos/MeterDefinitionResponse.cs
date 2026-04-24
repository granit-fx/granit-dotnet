using Granit.Metering.Domain;
using Granit.Workflow.Domain;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="MeterDefinition"/> for API responses.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Name">Meter display name.</param>
/// <param name="Unit">Unit of measure.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AggregationType">How events are aggregated into rollups.</param>
/// <param name="Activated">
/// [DEPRECATED] Computed compatibility alias — <c>true</c> when
/// <paramref name="LifecycleStatus"/> is <see cref="WorkflowLifecycleStatus.Published"/>.
/// Will be removed in the next major release; new clients should use
/// <paramref name="LifecycleStatus"/> directly.
/// </param>
/// <param name="ProductId">
/// Optional reference to a <c>Granit.Catalog.Product</c> identifier (soft, no SQL FK).
/// </param>
/// <param name="LifecycleStatus">
/// Current lifecycle status of the meter (Draft / Published / Archived). Only
/// <see cref="WorkflowLifecycleStatus.Published"/> meters accept ingestion.
/// </param>
/// <param name="DistinctProperty">
/// JSON property name inside <c>MeterEvent.Metadata</c> whose distinct values are
/// counted. Set when <see cref="AggregationType"/> is
/// <see cref="AggregationType.CountDistinct"/>; <c>null</c> for all others.
/// </param>
public sealed record MeterDefinitionResponse(
    Guid Id,
    string Name,
    string Unit,
    string? Description,
    AggregationType AggregationType,
    bool Activated,
    Guid? ProductId,
    WorkflowLifecycleStatus LifecycleStatus,
    string? DistinctProperty)
{
    internal static MeterDefinitionResponse FromEntity(MeterDefinition definition)
    {
#pragma warning disable CS0618 // Activated is intentionally surfaced for one-release compat.
        return new MeterDefinitionResponse(
            definition.Id,
            definition.Name,
            definition.Unit,
            definition.Description,
            definition.AggregationType,
            definition.Activated,
            definition.ProductId,
            definition.LifecycleStatus,
            definition.DistinctProperty);
#pragma warning restore CS0618
    }
}
