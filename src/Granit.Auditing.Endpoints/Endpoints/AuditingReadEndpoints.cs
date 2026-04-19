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
/// <remarks>
/// List and metadata endpoints (GET /, /meta, /saved-views/*) are provided by
/// <c>MapGranitQuery&lt;AuditEntry&gt;()</c>. Only lookups that have no query-engine
/// equivalent live here: by id, by entity reference, by correlation id.
/// </remarks>
internal static class AuditingReadEndpoints
{
    /// <summary>Maps all audit log read endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapAuditingReadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetAuditEntryById")
            .WithSummary("Returns a single audit log entry with full change details.")
            .WithDescription("Returns the complete audit log entry including all entity changes and property-level diffs (original → new value). Sensitive properties are masked with '***'. Returns 404 if the entry does not exist.")
            .Produces<AuditEntryDetailResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/entity/{entityType}/{entityId}", GetByEntityAsync)
            .WithName("GetAuditEntriesByEntity")
            .WithSummary("Returns the audit trail for a specific entity instance.")
            .WithDescription("Returns all audit log entries associated with a specific entity, identified by its CLR type name and primary key. Results are paginated and ordered by timestamp descending. Useful for displaying the full change history of a single record. Path parameters are limited to 256 characters.")
            .Produces<PagedResult<AuditEntryResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/correlation/{correlationId}", GetByCorrelationIdAsync)
            .WithName("GetAuditEntriesByCorrelationId")
            .WithSummary("Returns audit entries matching a distributed tracing correlation ID.")
            .WithDescription("Returns all audit log entries that share the given correlation ID, ordered by timestamp descending. This is essential for distributed tracing investigation — correlating audit events across multiple services or operations that belong to the same logical transaction. The correlation ID is limited to 256 characters.")
            .Produces<List<AuditEntryDetailResponse>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return group;
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

    private static async Task<Results<Ok<List<AuditEntryDetailResponse>>, ProblemHttpResult>> GetByCorrelationIdAsync(
        string correlationId,
        [FromServices] IAuditingReader reader,
        CancellationToken cancellationToken)
    {
        const int maxCorrelationIdLength = 256;
        if (string.IsNullOrWhiteSpace(correlationId) || correlationId.Length > maxCorrelationIdLength)
        {
            return TypedResults.Problem(
                detail: $"Correlation ID must be between 1 and {maxCorrelationIdLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        List<AuditEntry> entries = await reader
            .GetByCorrelationIdAsync(correlationId, cancellationToken).ConfigureAwait(false);

        var mapped = entries
            .Select(AuditingResponseMapper.ToDetailResponse)
            .ToList();

        return TypedResults.Ok(mapped);
    }

    private static async Task<Results<Ok<PagedResult<AuditEntryResponse>>, ProblemHttpResult>> GetByEntityAsync(
        string entityType,
        string entityId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromServices] IAuditingReader reader,
        CancellationToken cancellationToken)
    {
        const int maxPathParamLength = 256;
        if (entityType.Length > maxPathParamLength || entityId.Length > maxPathParamLength)
        {
            return TypedResults.Problem(
                detail: $"Path parameters must not exceed {maxPathParamLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

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
