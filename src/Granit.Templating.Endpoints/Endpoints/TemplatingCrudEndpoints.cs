using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Keys;
using Granit.Templating.Store;
using Granit.Users;
using Granit.Workflow.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Endpoints;

/// <summary>
/// CRUD Minimal API endpoints for template draft management:
/// GET detail, POST create, PUT update, DELETE draft.
/// </summary>
internal static class TemplatingCrudEndpoints
{
    /// <summary>Maps all template CRUD endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingCrudEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}", HandleGetDetailAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateDetail")
             .WithSummary("Returns detail of a template (current draft and published revision).")
             .WithDescription("Returns the full detail of a template including both the current draft revision (if any) and the published revision (if any). Includes content, metadata, category, and variable bindings. Returns 404 if the template name does not exist.")
             .Produces<TemplateDetailResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/", HandleCreateAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("CreateTemplateDraft")
             .WithSummary("Creates a new template draft.")
             .WithDescription("Creates a new template with an initial draft revision. The template name must be unique. The draft can be previewed and edited before publishing. Returns 201 Created with the template detail.")
             .Produces<TemplateDetailResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/{name}", HandleUpdateAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("UpdateTemplateDraft")
             .WithSummary("Updates an existing template draft.")
             .WithDescription("Replaces the draft revision content and metadata. Only the draft revision is affected — published and archived revisions are immutable. Creates a new draft if none exists. Returns 409 if the concurrency stamp does not match the stored draft. Returns 404 if the template does not exist.")
             .Produces<TemplateDetailResponse>()
             .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/{name}/draft", HandleDeleteDraftAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("DeleteTemplateDraft")
             .WithSummary("Deletes the draft revision of a template (published/archived are preserved).")
             .WithDescription("Removes only the draft revision. Published and archived revisions remain unaffected. Returns 404 if the template does not exist or has no draft revision.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET /{name} — Template detail
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateDetailResponse>, ProblemHttpResult>> HandleGetDetailAsync(
        HttpContext context,
        string name,
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

        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        if (draft is null && published is null)
        {
            return TypedResults.Problem(
                detail: "Template not found.",
                statusCode: StatusCodes.Status404NotFound);
        }

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == WorkflowLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = TemplatingResponseMapper.ToRevisionResponse(publishedRevision);
            }
        }

        return TypedResults.Ok(new TemplateDetailResponse(
            name,
            culture,
            draft is not null ? TemplatingResponseMapper.ToRevisionResponse(draft) : null,
            publishedResponse,
            draft?.LayoutName ?? published?.LayoutName));
    }

    // -------------------------------------------------------------------------
    // POST / — Create draft
    // -------------------------------------------------------------------------

    private static async Task<Results<Created<TemplateDetailResponse>, ProblemHttpResult>> HandleCreateAsync(
        HttpContext context,
        SaveTemplateRequest body,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();
        IDocumentTemplateStoreWriter? storeWriter =
            context.RequestServices.GetService<IDocumentTemplateStoreWriter>();

        if (storeReader is null || storeWriter is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return TypedResults.Problem(
                detail: "Template name is required when creating a new template.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(body.Name);
        if (nameError is not null)
        {
            return nameError;
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(body.Name, body.Culture);

        await storeWriter.SaveDraftAsync(key, body.Content, body.MimeType, userId, body.LayoutName, cancellationToken: cancellationToken).ConfigureAwait(false);

        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == WorkflowLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = TemplatingResponseMapper.ToRevisionResponse(publishedRevision);
            }
        }

        var response = new TemplateDetailResponse(
            body.Name,
            body.Culture,
            draft is not null ? TemplatingResponseMapper.ToRevisionResponse(draft) : null,
            publishedResponse,
            draft?.LayoutName);

        return TypedResults.Created($"{body.Name}", response);
    }

    // -------------------------------------------------------------------------
    // PUT /{name} — Update draft
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateDetailResponse>, ProblemHttpResult>> HandleUpdateAsync(
        HttpContext context,
        string name,
        SaveTemplateRequest body,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();
        IDocumentTemplateStoreWriter? storeWriter =
            context.RequestServices.GetService<IDocumentTemplateStoreWriter>();

        if (storeReader is null || storeWriter is null)
        {
            return TemplatingResponseMapper.StoreNotRegistered();
        }

        ProblemHttpResult? nameError = TemplatingResponseMapper.ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(name, body.Culture);

        await storeWriter.SaveDraftAsync(key, body.Content, body.MimeType, userId, body.LayoutName, body.ConcurrencyStamp, cancellationToken).ConfigureAwait(false);

        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == WorkflowLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = TemplatingResponseMapper.ToRevisionResponse(publishedRevision);
            }
        }

        return TypedResults.Ok(new TemplateDetailResponse(
            name,
            body.Culture,
            draft is not null ? TemplatingResponseMapper.ToRevisionResponse(draft) : null,
            publishedResponse,
            draft?.LayoutName));
    }

    // -------------------------------------------------------------------------
    // DELETE /{name}/draft — Delete draft
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteDraftAsync(
        HttpContext context,
        string name,
        string? culture,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreWriter? storeWriter =
            context.RequestServices.GetService<IDocumentTemplateStoreWriter>();

        if (storeWriter is null)
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

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(name, culture);

        try
        {
            await storeWriter.DeleteDraftAsync(key, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (Granit.Exceptions.NotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // Shared helper
    // -------------------------------------------------------------------------

    private static string GetCurrentUserId(HttpContext context)
    {
        ICurrentUserService? userService = context.RequestServices.GetService<ICurrentUserService>();
        return userService?.UserId
            ?? userService?.UserName
            ?? throw new UnauthorizedAccessException("Unable to resolve current user identity for audit trail.");
    }
}
