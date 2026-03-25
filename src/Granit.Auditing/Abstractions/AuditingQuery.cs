using Granit.Auditing.Domain;
using Granit.QueryEngine;

namespace Granit.Auditing.Abstractions;

/// <summary>
/// Query filter for paginated audit log retrieval.
/// </summary>
/// <param name="Page">One-based page number (default: 1).</param>
/// <param name="PageSize">Items per page (default: <see cref="QueryEngineDefaults.DefaultPageSize"/>).</param>
/// <param name="UserId">Filter by actor user identifier.</param>
/// <param name="EntityType">Filter by CLR entity type name.</param>
/// <param name="EntityId">Filter by entity primary key.</param>
/// <param name="Category">Filter by audit log category.</param>
/// <param name="From">Inclusive lower bound on <see cref="AuditEntry.Timestamp"/>.</param>
/// <param name="To">Exclusive upper bound on <see cref="AuditEntry.Timestamp"/>.</param>
public sealed record AuditingQuery(
    int Page = 1,
    int PageSize = QueryEngineDefaults.DefaultPageSize,
    string? UserId = null,
    string? EntityType = null,
    string? EntityId = null,
    AuditCategory? Category = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
