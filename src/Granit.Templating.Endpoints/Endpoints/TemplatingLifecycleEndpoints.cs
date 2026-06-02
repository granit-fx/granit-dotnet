using Granit.Exceptions;
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
/// Lifecycle Minimal API endpoints for templates:
/// GET lifecycle status, POST publish, POST unpublish.
/// </summary>
internal static class TemplatingLifecycleEndpoints
{
    /// <summary>Maps all template lifecycle endpoints to the given route group.</summary>
    public static RouteGroupBuilder MapTemplatingLifecycleEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/{name}/lifecycle", HandleGetLifecycleAsync)
             .RequireAuthorization(TemplatingPermissions.Templates.Read)
             .WithName("GetTemplateLifecycle")
             .WithSummary("Returns lifecycle status, workflow state, and available transitions.")
             .WithDescription("Returns the current lifecycle state of the template (draft, published, archived), the workflow state if workflow integration is enabled, and the list of available state transitions for the current user.")
             .Produces<TemplateLifecycleResponse>()
             .ProducesProblem(StatusCodes.Status404NotFound)
             .ProducesProblem(StatusCodes.Status400BadRequest)
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

        return group;
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

        // Determine the current status (Draft takes precedence for display)
        WorkflowLifecycleStatus currentStatus = draft is not null
            ? WorkflowLifecycleStatus.Draft
            : WorkflowLifecycleStatus.Published;

        ITemplateTransitionHook? hook = context.RequestServices.GetService<ITemplateTransitionHook>();
        bool workflowEnabled = hook?.IsWorkflowEnabled ?? false;

        // Compute available transitions from the current status
        List<WorkflowLifecycleStatus> availableTransitions = [];
        WorkflowLifecycleStatus[] possibleTargets =
        [
            WorkflowLifecycleStatus.Draft,
            WorkflowLifecycleStatus.PendingReview,
            WorkflowLifecycleStatus.Published,
            WorkflowLifecycleStatus.Archived,
        ];

        foreach (WorkflowLifecycleStatus target in possibleTargets)
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

        return TypedResults.Ok(
            await TemplatingResponseMapper.BuildDetailResponseAsync(storeReader, key, cancellationToken).ConfigureAwait(false));
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
