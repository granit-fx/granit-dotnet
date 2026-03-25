using Granit.AuditLog.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Granit.AuditLog.Endpoints.Dtos;

/// <summary>
/// Query parameters for the audit log list endpoint.
/// </summary>
public sealed class AuditLogQueryRequest
{
    /// <summary>One-based page number. Default: 1.</summary>
    [FromQuery(Name = "page")]
    public int? Page { get; set; }

    /// <summary>Items per page. Default: 20, Max: 100.</summary>
    [FromQuery(Name = "pageSize")]
    public int? PageSize { get; set; }

    /// <summary>Filter by actor user identifier.</summary>
    [FromQuery(Name = "userId")]
    public string? UserId { get; set; }

    /// <summary>Filter by CLR entity type name.</summary>
    [FromQuery(Name = "entityType")]
    public string? EntityType { get; set; }

    /// <summary>Filter by entity primary key.</summary>
    [FromQuery(Name = "entityId")]
    public string? EntityId { get; set; }

    /// <summary>Filter by audit log category.</summary>
    [FromQuery(Name = "category")]
    public AuditLogCategory? Category { get; set; }

    /// <summary>Inclusive lower bound on timestamp (UTC).</summary>
    [FromQuery(Name = "from")]
    public DateTimeOffset? From { get; set; }

    /// <summary>Exclusive upper bound on timestamp (UTC).</summary>
    [FromQuery(Name = "to")]
    public DateTimeOffset? To { get; set; }
}
