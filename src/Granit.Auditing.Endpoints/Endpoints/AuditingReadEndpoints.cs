using Granit.Auditing.Abstractions;
using Granit.Auditing.Domain;
using Granit.Auditing.Endpoints.Dtos;
using Granit.Auditing.Endpoints.Internal;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Auditing.Endpoints.Endpoints;

/// <summary>
/// Read-only Minimal API endpoints for the audit trail.
/// </summary>
internal static class AuditingReadEndpoints
{
    /// <summary>Maps all audit log read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapAuditingReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", GetPagedAsync)
            .WithName("GetAuditEntries")
            .WithSummary("List audit log entries with pagination and filters.")
            .WithDescription("Returns a paginated list of audit log entries ordered by timestamp descending. Supports filtering by actor (userId), entity type/ID, category, and date range. Each entry contains a summary with the number of entity changes — use the detail endpoint to retrieve full property-level diffs. ISO 27001 A.12.4 compliant.")
            .Produces<PagedResult<AuditEntryResponse>>();

        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetAuditEntryById")
            .WithSummary("Get a single audit log entry with full change details.")
            .WithDescription("Returns the complete audit log entry including all entity changes and property-level diffs (original → new value). Sensitive properties are masked with '***'. Returns 404 if the entry does not exist.")
            .Produces<AuditEntryDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/entity/{entityType}/{entityId}", GetByEntityAsync)
            .WithName("GetAuditEntriesByEntity")
            .WithSummary("Get audit trail for a specific entity instance.")
            .WithDescription("Returns all audit log entries associated with a specific entity, identified by its CLR type name and primary key. Results are paginated and ordered by timestamp descending. Useful for displaying the full change history of a single record.")
            .Produces<PagedResult<AuditEntryResponse>>();

        return group;
    }

    private static async Task<Ok<PagedResult<AuditEntryResponse>>> GetPagedAsync(
        [AsParameters] AuditingQueryParameters parameters,
        [FromServices] IAuditingReader reader,
        CancellationToken cancellationToken)
    {
        AuditingQuery query = new(
            Page: parameters.Page ?? 1,
            PageSize: Math.Clamp(parameters.PageSize ?? QueryEngineDefaults.DefaultPageSize, 1, QueryEngineDefaults.MaxPageSize),
            UserId: parameters.UserId,
            EntityType: parameters.EntityType,
            EntityId: parameters.EntityId,
            Category: parameters.Category,
            From: parameters.From,
            To: parameters.To);

        PagedResult<AuditEntry> result = await reader
            .GetPagedAsync(query, cancellationToken).ConfigureAwait(false);

        PagedResult<AuditEntryResponse> mapped = new(
            result.Items.Select(AuditingResponseMapper.ToSummaryResponse).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }

    private static async Task<Results<Ok<AuditEntryDetailResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IAuditingReader reader,
        CancellationToken cancellationToken)
    {
        AuditEntry? entry = await reader
            .GetByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (entry is null)
        {
            return TypedResults.Problem(
                detail: $"Audit log entry '{id}' not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(AuditingResponseMapper.ToDetailResponse(entry));
    }

    private static async Task<Ok<PagedResult<AuditEntryResponse>>> GetByEntityAsync(
        string entityType,
        string entityId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] IAuditingReader reader,
        CancellationToken cancellationToken)
    {
        PagedResult<AuditEntry> result = await reader
            .GetByEntityAsync(
                entityType,
                entityId,
                page ?? 1,
                Math.Clamp(pageSize ?? QueryEngineDefaults.DefaultPageSize, 1, QueryEngineDefaults.MaxPageSize),
                cancellationToken)
            .ConfigureAwait(false);

        PagedResult<AuditEntryResponse> mapped = new(
            result.Items.Select(AuditingResponseMapper.ToSummaryResponse).ToList(),
            result.TotalCount,
            result.HasMore);

        return TypedResults.Ok(mapped);
    }
}
