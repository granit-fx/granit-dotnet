using Granit.Metering.Domain;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>Request to create a new meter definition.</summary>
/// <param name="Name">Meter display name (e.g., "API Calls").</param>
/// <param name="Unit">Unit of measure (e.g., "requests", "GB").</param>
/// <param name="AggregationType">How events are aggregated into rollups.</param>
/// <param name="Description">Optional description.</param>
/// <param name="ProductId">
/// Optional reference to a <c>Granit.Catalog.Product</c> identifier — the catalog
/// item this meter measures. Soft reference (no SQL FK across modules).
/// </param>
/// <param name="DistinctProperty">
/// JSON property name inside <c>MeterEvent.Metadata</c> whose distinct values are
/// counted. <strong>Required</strong> when <see cref="AggregationType"/> is
/// <see cref="AggregationType.CountDistinct"/>; must be <c>null</c> for all others.
/// </param>
public sealed record MeterDefinitionCreateRequest(
    string Name,
    string Unit,
    AggregationType AggregationType,
    string? Description = null,
    Guid? ProductId = null,
    string? DistinctProperty = null);
