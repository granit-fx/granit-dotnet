// ---------------------------------------------------------------------------
// TemplatingEndpointRouteBuilderExtensions.cs
// Minimal API extensions for Granit template administration:
//   - MapGranitTemplatingAdmin: CRUD endpoints for template draft management
//     (requires Templates.Manage permission)
// ---------------------------------------------------------------------------

using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Granit.Exceptions;
using Granit.Templating.Endpoints.Dtos;
using Granit.Templating.Endpoints.Internal;
using Granit.Templating.Endpoints.Options;
using Granit.Templating.Endpoints.Permissions;
using Granit.Templating.Exceptions;
using Granit.Templating.GlobalContext;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Users;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating.Endpoints.Extensions;

/// <summary>
/// Extension methods for mapping Granit template administration endpoints.
/// </summary>
public static class TemplatingEndpointRouteBuilderExtensions
{

    /// <summary>
    /// Maps template administration endpoints under <c>/{prefix}</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers 16 endpoints:
    /// <list type="bullet">
    /// <item><c>GET /</c> — paginated list with filters (including <c>categoryId</c>)</item>
    /// <item><c>GET /{name}</c> — detail (draft + published)</item>
    /// <item><c>POST /</c> — create a new draft</item>
    /// <item><c>PUT /{name}</c> — update an existing draft</item>
    /// <item><c>DELETE /{name}/draft</c> — delete draft only</item>
    /// <item><c>POST /{name}/publish</c> — publish the current draft</item>
    /// <item><c>POST /{name}/unpublish</c> — unpublish (archive the published revision)</item>
    /// <item><c>GET /{name}/lifecycle</c> — lifecycle info (current status, available transitions)</item>
    /// <item><c>POST /{name}/preview</c> — render the current draft with test data</item>
    /// <item><c>GET /{name}/variables</c> — list available template variables for autocompletion</item>
    /// <item><c>GET /{name}/history</c> — paginated revision history (summaries, no content)</item>
    /// <item><c>GET /{name}/history/{revisionId}</c> — full detail of a specific revision</item>
    /// <item><c>GET /categories</c> — list all template categories</item>
    /// <item><c>POST /categories</c> — create a new category</item>
    /// <item><c>PUT /categories/{id}</c> — update a category</item>
    /// <item><c>DELETE /categories/{id}</c> — delete a category (409 if templates associated)</item>
    /// </list>
    /// </para>
    /// <para>
    /// All endpoints require the <c>Templates.Manage</c> permission.
    /// If <see cref="IDocumentTemplateStoreReader"/>/<see cref="IDocumentTemplateStoreWriter"/> is not registered
    /// (no EF Core persistence module loaded), all endpoints return <c>501 Not Implemented</c>.
    /// </para>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="TemplatingEndpointsOptions"/>.</param>
    /// <returns>The route group builder for further chaining.</returns>
    public static RouteGroupBuilder MapGranitTemplatingAdmin(
        this IEndpointRouteBuilder endpoints,
        Action<TemplatingEndpointsOptions>? configure = null)
    {
        TemplatingEndpointsOptions options = new();
        configure?.Invoke(options);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup(options.RoutePrefix)
            .WithTags(options.TagName);

        // ----- Read endpoints (Templates.Read) -----

        group.MapGet("/", HandleListAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("ListTemplates")
             .WithSummary("Returns a paginated list of templates with filters.")
             .WithDescription("Returns templates with optional filtering by category, status, and search term. Each item includes the template name, current lifecycle status, category, and last modification date. Content is not included — use the detail endpoint for full content.")
             .Produces<TemplateListResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{name}", HandleGetDetailAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateDetail")
             .WithSummary("Returns detail of a template (current draft and published revision).")
             .WithDescription("Returns the full detail of a template including both the current draft revision (if any) and the published revision (if any). Includes content, metadata, category, and variable bindings. Returns 404 if the template name does not exist.")
             .Produces<TemplateDetailResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{name}/lifecycle", HandleGetLifecycleAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateLifecycle")
             .WithSummary("Returns lifecycle status, workflow state, and available transitions.")
             .WithDescription("Returns the current lifecycle state of the template (draft, published, archived), the workflow state if workflow integration is enabled, and the list of available state transitions for the current user.")
             .Produces<TemplateLifecycleResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapGet("/{name}/variables", HandleGetVariablesAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateVariables")
             .WithSummary("Returns all available template variables (global, model, enriched) for autocompletion.")
             .WithDescription("Returns all variables available for use in the template: global variables (application-wide), model variables (bound to the template's entity type), and enriched variables (computed at render time). Used to power autocompletion in the template editor.")
             .Produces<TemplateVariablesResponse>()
             .ProducesProblem(StatusCodes.Status400BadRequest);

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

        // ----- Write endpoints (Templates.Manage) -----

        group.MapPost("/", HandleCreateAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("CreateTemplateDraft")
             .WithSummary("Creates a new template draft.")
             .WithDescription("Creates a new template with an initial draft revision. The template name must be unique. The draft can be previewed and edited before publishing. Returns 201 Created with the template detail.")
             .Produces<TemplateDetailResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/{name}", HandleUpdateAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("UpdateTemplateDraft")
             .WithSummary("Updates an existing template draft.")
             .WithDescription("Replaces the draft revision content and metadata. Only the draft revision is affected — published and archived revisions are immutable. Creates a new draft if none exists. Returns 404 if the template does not exist.")
             .Produces<TemplateDetailResponse>()
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status400BadRequest)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/{name}/draft", HandleDeleteDraftAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("DeleteTemplateDraft")
             .WithSummary("Deletes the draft revision of a template (published/archived are preserved).")
             .WithDescription("Removes only the draft revision. Published and archived revisions remain unaffected. Returns 404 if the template does not exist or has no draft revision.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/{name}/publish", HandlePublishAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("PublishTemplate")
             .WithSummary("Publishes the current draft, archiving any previous published revision.")
             .WithDescription("Promotes the current draft to published status. If a published revision already exists, it is archived first. The draft is consumed — a new draft must be created for further edits. Returns 404 if the template or draft does not exist.")
             .Produces<TemplateDetailResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/{name}/unpublish", HandleUnpublishAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("UnpublishTemplate")
             .WithSummary("Unpublishes the template (archives the published revision).")
             .WithDescription("Archives the currently published revision. The template will no longer be available for rendering until a new revision is published. Returns 404 if the template has no published revision.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPost("/{name}/preview", HandlePreviewAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Manage)
             .WithName("PreviewTemplate")
             .WithSummary("Renders the current draft with optional test data and returns the HTML output.")
             .WithDescription("Renders the template's current draft content using the configured template engine (Liquid, Razor, etc.) with optional test data. Returns the rendered HTML. Useful for live preview in the template editor. Returns 404 if the template or draft does not exist.")
             .Produces<TemplatePreviewResponse>()
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        // ----- Categories — Read (Categories.Read) -----

        group.MapGet("/categories", HandleListCategoriesAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Read)
             .WithName("ListTemplateCategories")
             .WithSummary("Returns all template categories ordered by sort order then name.")
             .WithDescription("Returns all template categories for organizing templates. Categories are sorted by their sort order, then alphabetically by name. Each category includes its ID, name, and template count.")
             .Produces<IReadOnlyList<TemplateCategoryResponse>>()
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        // ----- Categories — Write (Categories.Manage) -----

        group.MapPost("/categories", HandleCreateCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("CreateTemplateCategory")
             .WithSummary("Creates a new template category.")
             .WithDescription("Creates a new template category with the given name and sort order. The name must be unique.")
             .Produces<TemplateCategoryResponse>(StatusCodes.Status201Created)
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapPut("/categories/{id:guid}", HandleUpdateCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("UpdateTemplateCategory")
             .WithSummary("Updates an existing template category.")
             .WithDescription("Updates the name and sort order of an existing category. Returns 404 if the category does not exist.")
             .Produces<TemplateCategoryResponse>()
             .ProducesValidationProblem()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        group.MapDelete("/categories/{id:guid}", HandleDeleteCategoryAsync)
             .RequireAuthorization(TemplatingPermissions.Categories.Manage)
             .WithName("DeleteTemplateCategory")
             .WithSummary("Deletes a template category (409 if templates are still associated).")
             .WithDescription("Deletes the template category. Returns 409 Conflict if templates are still associated with this category — reassign or delete them first. Returns 404 if the category does not exist.")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status409Conflict)
             .ProducesProblem(StatusCodes.Status501NotImplemented);

        return group;
    }

    // -------------------------------------------------------------------------
    // GET / — List templates
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateListResponse>, ProblemHttpResult>> HandleListAsync(
        HttpContext context,
        [AsParameters] TemplateListQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? error = ValidatePagination(parameters.Page, parameters.PageSize);
        if (error is not null)
        {
            return error;
        }

        if (parameters.Culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(parameters.Culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        TemplateListFilter filter = new(
            Page: parameters.Page,
            PageSize: parameters.PageSize,
            Search: parameters.Search,
            Status: parameters.Status,
            Culture: parameters.Culture,
            CategoryId: parameters.CategoryId);

        PagedTemplateResult result = await storeReader.ListTemplatesAsync(filter, cancellationToken).ConfigureAwait(false);

        var items = result.Items
            .Select(s => new TemplateListItemResponse(
                s.Name,
                s.Culture,
                s.MimeType,
                s.CurrentStatus,
                s.LastModifiedAt,
                s.LastModifiedBy,
                s.HasPublishedVersion))
            .ToList();

        return TypedResults.Ok(new TemplateListResponse(items, result.TotalCount));
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
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
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

        // For published, we need the full revision to build the response.
        // TryGetPublishedAsync returns a TemplateDescriptor (without metadata).
        // Use GetHistoryAsync to find the published revision with full metadata.
        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == TemplateLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = ToRevisionResponse(publishedRevision);
            }
        }

        return TypedResults.Ok(new TemplateDetailResponse(
            name,
            culture,
            draft is not null ? ToRevisionResponse(draft) : null,
            publishedResponse));
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
            return StoreNotRegistered();
        }

        if (string.IsNullOrWhiteSpace(body.Name))
        {
            return TypedResults.Problem(
                detail: "Template name is required when creating a new template.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        ProblemHttpResult? nameError = ValidateTemplateName(body.Name);
        if (nameError is not null)
        {
            return nameError;
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(body.Name, body.Culture);

        await storeWriter.SaveDraftAsync(key, body.Content, body.MimeType, userId, cancellationToken).ConfigureAwait(false);

        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == TemplateLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = ToRevisionResponse(publishedRevision);
            }
        }

        var response = new TemplateDetailResponse(
            body.Name,
            body.Culture,
            draft is not null ? ToRevisionResponse(draft) : null,
            publishedResponse);

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
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(name, body.Culture);

        await storeWriter.SaveDraftAsync(key, body.Content, body.MimeType, userId, cancellationToken).ConfigureAwait(false);

        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == TemplateLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = ToRevisionResponse(publishedRevision);
            }
        }

        return TypedResults.Ok(new TemplateDetailResponse(
            name,
            body.Culture,
            draft is not null ? ToRevisionResponse(draft) : null,
            publishedResponse));
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
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
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
        catch (NotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /{name}/publish — Publish the current draft
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateDetailResponse>, ProblemHttpResult>> HandlePublishAsync(
        HttpContext context,
        string name,
        string? culture,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();
        IDocumentTemplateStoreWriter? storeWriter =
            context.RequestServices.GetService<IDocumentTemplateStoreWriter>();

        if (storeReader is null || storeWriter is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(name, culture);

        try
        {
            await storeWriter.PublishAsync(key, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (NotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }

        return TypedResults.Ok(await BuildDetailResponseAsync(storeReader, key, cancellationToken).ConfigureAwait(false));
    }

    // -------------------------------------------------------------------------
    // POST /{name}/unpublish — Unpublish (archive the published revision)
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleUnpublishAsync(
        HttpContext context,
        string name,
        string? culture,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreWriter? storeWriter =
            context.RequestServices.GetService<IDocumentTemplateStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        string userId = GetCurrentUserId(context);
        TemplateKey key = new(name, culture);

        try
        {
            await storeWriter.UnpublishAsync(key, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /{name}/lifecycle — Lifecycle status and available transitions
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateLifecycleResponse>, ProblemHttpResult>> HandleGetLifecycleAsync(
        HttpContext context,
        string name,
        string? culture,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
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

        // Determine the current status (Draft takes precedence for display)
        TemplateLifecycleStatus currentStatus = draft is not null
            ? TemplateLifecycleStatus.Draft
            : TemplateLifecycleStatus.Published;

        ITemplateTransitionHook? hook = context.RequestServices.GetService<ITemplateTransitionHook>();
        bool workflowEnabled = hook?.IsWorkflowEnabled ?? false;

        // Compute available transitions from the current status
        List<TemplateLifecycleStatus> availableTransitions = [];
        TemplateLifecycleStatus[] possibleTargets =
        [
            TemplateLifecycleStatus.Draft,
            TemplateLifecycleStatus.PendingReview,
            TemplateLifecycleStatus.Published,
            TemplateLifecycleStatus.Archived,
        ];

        foreach (TemplateLifecycleStatus target in possibleTargets)
        {
            if (target == currentStatus)
            {
                continue;
            }

            if (hook is not null &&
                await hook.CanTransitionAsync(currentStatus, target, cancellationToken).ConfigureAwait(false))
            {
                availableTransitions.Add(target);
            }
        }

        return TypedResults.Ok(new TemplateLifecycleResponse(
            name,
            culture,
            currentStatus,
            workflowEnabled,
            availableTransitions));
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
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        ProblemHttpResult? paginationError = ValidatePagination(page, pageSize);
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
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(culture);
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

        return TypedResults.Ok(ToRevisionResponse(revision));
    }

    // -------------------------------------------------------------------------
    // POST /{name}/preview — Render the current draft with test data
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplatePreviewResponse>, NotFound, ProblemHttpResult>> HandlePreviewAsync(
        HttpContext context,
        string name,
        TemplatePreviewRequest body,
        CancellationToken cancellationToken)
    {
        IDocumentTemplateStoreReader? storeReader =
            context.RequestServices.GetService<IDocumentTemplateStoreReader>();

        if (storeReader is null)
        {
            return StoreNotRegistered();
        }

        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return nameError;
        }

        if (body.Culture is not null)
        {
            ProblemHttpResult? cultureError = ValidateBcp47(body.Culture);
            if (cultureError is not null)
            {
                return cultureError;
            }
        }

        TemplateKey key = new(name, body.Culture);
        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);

        if (draft is null)
        {
            return TypedResults.NotFound();
        }

        var engines =
            context.RequestServices.GetServices<ITemplateEngine>().ToList();

        if (engines.Count == 0)
        {
            return TypedResults.Problem(
                detail: "No template engine is registered. Add Granit.Templating.Scriban to enable rendering.",
                statusCode: StatusCodes.Status501NotImplemented);
        }

        TemplateDescriptor descriptor = new()
        {
            Content = draft.Content,
            MimeType = draft.MimeType,
            RevisionId = draft.RevisionId,
        };

        ITemplateEngine? engine = engines.FirstOrDefault(e => e.CanRender(descriptor));
        if (engine is null)
        {
            return TypedResults.Problem(
                detail: $"No template engine can render MIME type '{draft.MimeType}'.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        IEnumerable<ITemplateGlobalContext> globalContexts =
            context.RequestServices.GetServices<ITemplateGlobalContext>();

        Dictionary<string, object?> data = body.Data.HasValue
            ? ConvertJsonObject(body.Data.Value)
            : [];

        var sw = Stopwatch.StartNew();

        RenderedContent rendered;
        try
        {
            rendered = await engine.RenderAsync(
                descriptor,
                data,
                DocumentFormat.Html,
                globalContexts.ToList(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Do not leak internal error details (VULN-300). Log the full exception
            // via structured logging in the engine; return a generic message to the client.
            return TypedResults.Problem(
                detail: "Template rendering failed. Check the template syntax and data model.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        sw.Stop();

        if (rendered is not TextRenderedContent textContent)
        {
            return TypedResults.Problem(
                detail: "Preview is only supported for text-based templates (HTML). Binary templates (Excel) cannot be previewed.",
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        return TypedResults.Ok(new TemplatePreviewResponse(
            textContent.Html,
            rendered.RevisionId,
            sw.ElapsedMilliseconds));
    }

    // -------------------------------------------------------------------------
    // GET /{name}/variables — Available template variables
    // -------------------------------------------------------------------------

    private static Task<Results<Ok<TemplateVariablesResponse>, ProblemHttpResult>> HandleGetVariablesAsync(
        HttpContext context,
        string name)
    {
        ProblemHttpResult? nameError = ValidateTemplateName(name);
        if (nameError is not null)
        {
            return Task.FromResult<Results<Ok<TemplateVariablesResponse>, ProblemHttpResult>>(nameError);
        }

        // Global variables — discovered by reflecting on ITemplateGlobalContext.Resolve() return types
        var globalContexts =
            context.RequestServices.GetServices<ITemplateGlobalContext>().ToList();

        List<TemplateVariableItemResponse> globalVariables = [];
        foreach (ITemplateGlobalContext globalContext in globalContexts)
        {
            object resolved = globalContext.Resolve();
            Type resolvedType = resolved.GetType();

            foreach (PropertyInfo property in resolvedType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                string variableName = $"{globalContext.ContextName}.{ToSnakeCase(property.Name)}";
                string typeName = MapClrTypeName(property.PropertyType);
                globalVariables.Add(new TemplateVariableItemResponse(variableName, typeName, null));
            }
        }

        // Model and enriched variables are not yet discoverable at runtime.
        // TemplateType<TData> instances are static singletons, not registered in DI.
        // A future ITemplateTypeRegistry could enable model variable introspection.
        var response = new TemplateVariablesResponse(
            globalVariables,
            ModelVariables: [],
            EnrichedVariables: []);

        return Task.FromResult<Results<Ok<TemplateVariablesResponse>, ProblemHttpResult>>(
            TypedResults.Ok(response));
    }

    // -------------------------------------------------------------------------
    // GET /categories — List all categories
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<IReadOnlyList<TemplateCategoryResponse>>, ProblemHttpResult>> HandleListCategoriesAsync(
        HttpContext context,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreReader? storeReader =
            context.RequestServices.GetService<ITemplateCategoryStoreReader>();

        if (storeReader is null)
        {
            return StoreNotRegistered();
        }

        IReadOnlyList<TemplateCategory> categories =
            await storeReader.ListCategoriesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<TemplateCategoryResponse> response = categories
            .Select(ToCategoryResponse)
            .ToList();

        return TypedResults.Ok(response);
    }

    // -------------------------------------------------------------------------
    // POST /categories — Create a category
    // -------------------------------------------------------------------------

    private static async Task<Results<Created<TemplateCategoryResponse>, ProblemHttpResult>> HandleCreateCategoryAsync(
        HttpContext context,
        SaveTemplateCategoryRequest body,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        string userId = GetCurrentUserId(context);

        TemplateCategory category;
        try
        {
            category = await storeWriter.CreateCategoryAsync(
                body.Name, body.Description, body.Icon, body.SortOrder, userId, cancellationToken).ConfigureAwait(false);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Created($"categories/{category.Id}", ToCategoryResponse(category));
    }

    // -------------------------------------------------------------------------
    // PUT /categories/{id} — Update a category
    // -------------------------------------------------------------------------

    private static async Task<Results<Ok<TemplateCategoryResponse>, ProblemHttpResult>> HandleUpdateCategoryAsync(
        HttpContext context,
        Guid id,
        SaveTemplateCategoryRequest body,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        TemplateCategory category;
        try
        {
            category = await storeWriter.UpdateCategoryAsync(
                id, body.Name, body.Description, body.Icon, body.SortOrder, cancellationToken).ConfigureAwait(false);
        }
        catch (EntityNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.Ok(ToCategoryResponse(category));
    }

    // -------------------------------------------------------------------------
    // DELETE /categories/{id} — Delete a category
    // -------------------------------------------------------------------------

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleDeleteCategoryAsync(
        HttpContext context,
        Guid id,
        CancellationToken cancellationToken)
    {
        ITemplateCategoryStoreWriter? storeWriter =
            context.RequestServices.GetService<ITemplateCategoryStoreWriter>();

        if (storeWriter is null)
        {
            return StoreNotRegistered();
        }

        try
        {
            await storeWriter.DeleteCategoryAsync(id, cancellationToken).ConfigureAwait(false);
        }
        catch (EntityNotFoundException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (ConflictException ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status409Conflict);
        }

        return TypedResults.NoContent();
    }

    private static TemplateCategoryResponse ToCategoryResponse(TemplateCategory category) =>
        new(category.Id, category.Name, category.Description, category.Icon,
            category.SortOrder, category.TemplateCount);

    // -------------------------------------------------------------------------
    // Shared helpers
    // -------------------------------------------------------------------------

    private static string GetCurrentUserId(HttpContext context)
    {
        ICurrentUserService? userService = context.RequestServices.GetService<ICurrentUserService>();
        return userService?.UserId
            ?? userService?.UserName
            ?? throw new UnauthorizedAccessException("Unable to resolve current user identity for audit trail.");
    }

    private static TemplateRevisionResponse ToRevisionResponse(TemplateRevision revision) =>
        new(
            revision.RevisionId,
            revision.Content,
            revision.MimeType,
            revision.Status,
            revision.CreatedAt,
            revision.CreatedBy,
            revision.PublishedAt,
            revision.PublishedBy);

    private static async Task<TemplateDetailResponse> BuildDetailResponseAsync(
        IDocumentTemplateStoreReader storeReader,
        TemplateKey key,
        CancellationToken cancellationToken)
    {
        TemplateRevision? draft = await storeReader.TryGetDraftAsync(key, cancellationToken).ConfigureAwait(false);
        Pipeline.TemplateDescriptor? published = await storeReader.TryGetPublishedAsync(key, cancellationToken).ConfigureAwait(false);

        TemplateRevisionResponse? publishedResponse = null;
        if (published is not null)
        {
            IReadOnlyList<TemplateRevision> history = await storeReader.GetHistoryAsync(key, cancellationToken).ConfigureAwait(false);
            TemplateRevision? publishedRevision = history.FirstOrDefault(
                r => r.Status == TemplateLifecycleStatus.Published);

            if (publishedRevision is not null)
            {
                publishedResponse = ToRevisionResponse(publishedRevision);
            }
        }

        return new TemplateDetailResponse(
            key.Name,
            key.Culture,
            draft is not null ? ToRevisionResponse(draft) : null,
            publishedResponse);
    }

    private static ProblemHttpResult StoreNotRegistered() =>
        TypedResults.Problem(
            detail: "No template store is registered. Install a persistence module to enable this feature.",
            statusCode: StatusCodes.Status501NotImplemented);

    private static ProblemHttpResult? ValidateTemplateName(string name)
    {
        if (name.Length > TemplatingPatterns.MaxNameLength)
        {
            return TypedResults.Problem(
                detail: $"Template name must not exceed {TemplatingPatterns.MaxNameLength} characters.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!TemplatingPatterns.TemplateNamePattern().IsMatch(name))
        {
            return TypedResults.Problem(
                detail: "Template name must follow the 'Domain.Name' pattern (e.g. 'Billing.Invoice').",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    private static ProblemHttpResult? ValidateBcp47(string cultureName) =>
        TemplatingPatterns.Bcp47Pattern().IsMatch(cultureName)
            ? null
            : TypedResults.Problem(
                detail: $"Culture name '{cultureName}' is not a valid BCP 47 tag.",
                statusCode: StatusCodes.Status400BadRequest);

    private static ProblemHttpResult? ValidatePagination(int page, int pageSize)
    {
        if (page is < 1 or > 10_000)
        {
            return TypedResults.Problem(
                detail: "Page must be between 1 and 10000.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (pageSize is < 1 or > 100)
        {
            return TypedResults.Problem(
                detail: "PageSize must be between 1 and 100.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return null;
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> object to a <see cref="Dictionary{TKey, TValue}"/>
    /// suitable for Scriban template rendering.
    /// </summary>
    private static Dictionary<string, object?> ConvertJsonObject(JsonElement element)
    {
        Dictionary<string, object?> dict = [];

        if (element.ValueKind != JsonValueKind.Object)
        {
            return dict;
        }

        foreach (JsonProperty property in element.EnumerateObject())
        {
            dict[property.Name] = ConvertJsonValue(property.Value);
        }

        return dict;
    }

    private static object? ConvertJsonValue(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    /// <summary>
    /// Converts a PascalCase property name to snake_case.
    /// Replicates Scriban's <c>StandardMemberRenamer.Default</c> behavior.
    /// </summary>
    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        StringBuilder sb = new();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string MapClrTypeName(Type type)
    {
        Type underlying = Nullable.GetUnderlyingType(type) ?? type;

        if (underlying == typeof(string))
        {
            return "string";
        }

        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(short) || underlying == typeof(byte) ||
            underlying == typeof(decimal) || underlying == typeof(double) ||
            underlying == typeof(float))
        {
            return "number";
        }

        if (underlying == typeof(bool))
        {
            return "boolean";
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset) ||
            underlying == typeof(DateOnly))
        {
            return "date";
        }

        if (underlying == typeof(TimeOnly) || underlying == typeof(TimeSpan))
        {
            return "time";
        }

        if (underlying == typeof(Guid))
        {
            return "string";
        }

        return "object";
    }
}
