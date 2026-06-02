using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Keys;
using Granit.Templating.Store;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Endpoints;

/// <summary>
/// Revision history Minimal API endpoints for templates:
/// paginated history list and single revision detail.
/// </summary>
internal static class TemplatingHistoryEndpoints
{
    /// <summary>Maps all template history endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingHistoryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/history", HandleGetHistoryAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateHistory")
             .WithSummary("Returns a paginated revision history for the template (without content).")
             .WithDescription("Returns a paginated list of all revisions for the template, ordered by creation date descending. Each entry includes revision ID, status, author, and timestamp — but not the content. Use the revision detail endpoint to retrieve content.")
             .Produces<TemplateHistoryResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{name}/history/{revisionId:guid}", HandleGetRevisionDetailAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateRevisionDetail")
             .WithSummary("Returns the full detail of a specific template revision (including content).")
             .WithDescription("Returns the complete content and metadata of a specific historical revision, identified by its GUID. Useful for comparing versions or restoring a previous revision. Returns 404 if the revision does not exist.")
             .Produces<TemplateRevisionResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET /{name}/history — Paginated revision history (summaries)
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateHistoryResponse>, ProblemHttpResult>> HandleGetHistoryAsync(
        HttpContext context,
        string name,
        string? culture,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = TemplatingResponseMapper.ValidateBcp47(culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        ProblemHttpResult? paginationError = TemplatingResponseMapper.ValidatePagination(page, pageSize);
        if (paginationError is not null)
        {
            return paginationError;
        }

        TemplateKey key = new(name, culture);
        IReadOnlyList<TemplateRevision> allRevisions =
            await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);

        int totalCount = allRevisions.Count;
        var summaries = allRevisions
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new TemplateRevisionSummaryResponse(
                r.RevisionId,
                r.Status,
                r.CreatedAt,
                r.CreatedBy,
                r.PublishedAt,
                r.PublishedBy,
                r.Content.Length))
            .ToList();

        return TypedResults.Ok(new TemplateHistoryResponse(summaries, totalCount, page, pageSize));
    }

    // -------------------------------------------------------------------------
    // GET /{name}/history/{revisionId} — Full detail of a specific revision
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateRevisionResponse>, ProblemHttpResult>> HandleGetRevisionDetailAsync(
        HttpContext context,
        string name,
        Guid revisionId,
        string? culture,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = TemplatingResponseMapper.ValidateBcp47(culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        TemplateKey key = new(name, culture);
        IReadOnlyList<TemplateRevision> history =
            await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevision? revision = history.FirstOrDefault(r => r.RevisionId == revisionId);
        if (revision is null)
        {
            return TypedResults.Problem(
                detail: "Template revision not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(TemplatingResponseMapper.ToRevisionResponse(revision));
    }
}
