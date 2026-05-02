using Granit.Documents.Domain;
using Granit.Documents.Endpoints.Folders.Dtos;
using Granit.Documents.Endpoints.Folders.Mapping;
using Granit.Documents.Endpoints.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Granit.Documents.Endpoints.Folders.Endpoints;

/// <summary>
/// HTTP endpoints for folder hierarchy CRUD + breadcrumb (F2.3).
/// </summary>
internal static class FolderEndpoints
{
    private const string TagName = "Documents - Folders";

    public static RouteGroupBuilder MapFolderEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        RouteGroupBuilder folders = group.MapGroup("/folders").WithTags(TagName);

        folders.MapGet("/", ListChildrenAsync)
            .WithName("ListFolders")
            .WithSummary("Lists active child folders under a parent (defaults to the tenant root).")
            .WithDescription(
                "Returns the active children of the folder identified by the optional `parentId` query parameter. "
                + "When `parentId` is omitted, the children of the invisible tenant root are returned. "
                + "The tenant root itself is always filtered out of the result set. "
                + "Trashed folders are excluded — use the trash listing endpoint (F8.2) to retrieve them.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Read))
            .Produces<ListFoldersResponse>();

        folders.MapGet("/{id:guid}", GetByIdAsync)
            .WithName("GetFolder")
            .WithSummary("Returns a folder by id.")
            .WithDescription(
                "Returns the folder identified by `id`, including the materialised path and depth. "
                + "Returns 404 when the folder does not exist or when the caller's tenant filter "
                + "excludes it. The tenant root itself is also returned as 404 because it is not "
                + "user-visible.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Read))
            .Produces<FolderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        folders.MapGet("/{id:guid}/breadcrumb", GetBreadcrumbAsync)
            .WithName("GetFolderBreadcrumb")
            .WithSummary("Returns the chain of ancestors plus the folder itself.")
            .WithDescription(
                "Returns the chain of folders from the first user-visible ancestor (closest to the "
                + "tenant root) down to and including the folder identified by `id`. The invisible "
                + "tenant root is excluded from the chain so the client never has to know about it. "
                + "Returns 404 when the folder does not exist or is the tenant root.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Read))
            .Produces<FolderBreadcrumbResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        folders.MapPost("/", CreateAsync)
            .WithName("CreateFolder")
            .WithSummary("Creates a folder under the given parent (or under the tenant root).")
            .WithDescription(
                "Creates a new folder under `parentFolderId`. When `parentFolderId` is omitted the "
                + "folder is created directly under the invisible tenant root, which is auto-bootstrapped "
                + "on first access for the tenant. The folder name is validated against length, the "
                + "reserved path separator '/', and parent-scoped uniqueness.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Manage))
            .Produces<FolderResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        folders.MapPatch("/{id:guid}", RenameAsync)
            .WithName("RenameFolder")
            .WithSummary("Renames a folder.")
            .WithDescription(
                "Renames the folder identified by `id`. The materialised path is recomputed atomically. "
                + "Descendant paths are updated by the move-folder endpoint (F2.4) — rename does not "
                + "modify descendants. The tenant root cannot be renamed.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Manage))
            .Produces<FolderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem();

        folders.MapPost("/{id:guid}/move", MoveAsync)
            .WithName("MoveFolder")
            .WithSummary("Moves a folder under a new parent.")
            .WithDescription(
                "Moves the folder identified by `id` under `newParentFolderId` (or under the "
                + "invisible tenant root when omitted). The materialised path and depth of the "
                + "moved folder AND every descendant are re-materialised in a single SQL "
                + "UPDATE within the same transaction. Cycles, cross-tenant moves, and moves "
                + "under a trashed or descendant target are rejected. The tenant root cannot "
                + "be moved.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Manage))
            .Produces<FolderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        folders.MapDelete("/{id:guid}", TrashAsync)
            .WithName("TrashFolder")
            .WithSummary("Sends a folder to the trash (soft-delete).")
            .WithDescription(
                "Moves the folder identified by `id` to the trash. Permanent deletion happens after "
                + "the configured retention period via the empty-trash background job (F8/F9.2). "
                + "The tenant root cannot be trashed.")
            .RequireAuthorization(p => p.RequireClaim("permission", DocumentsPermissions.Folders.Manage))
            .Produces<FolderResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return folders;
    }

    // -------------------------------------------------------------------------
    // Handlers
    // -------------------------------------------------------------------------

    private static async Task<Ok<ListFoldersResponse>> ListChildrenAsync(
        [FromQuery] Guid? parentId,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Folder> children = await folders
            .ListChildrenAsync(parentId, cancellationToken)
            .ConfigureAwait(false);

        FolderResponse[] mapped = [.. children.Select(f => f.ToResponse())];
        return TypedResults.Ok(new ListFoldersResponse(mapped));
    }

    private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> GetByIdAsync(
        Guid id,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        Folder? folder = await folders.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (folder is null || folder.IsTenantRoot)
        {
            return TypedResults.Problem(
                $"Folder '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.Ok(folder.ToResponse());
    }

    private static async Task<Results<Ok<FolderBreadcrumbResponse>, ProblemHttpResult>> GetBreadcrumbAsync(
        Guid id,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Folder> chain = await folders
            .GetBreadcrumbAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (chain.Count == 0)
        {
            return TypedResults.Problem(
                $"Folder '{id}' was not found or is the tenant root.",
                statusCode: StatusCodes.Status404NotFound);
        }

        FolderResponse[] mapped = [.. chain.Select(f => f.ToResponse())];
        return TypedResults.Ok(new FolderBreadcrumbResponse(mapped));
    }

    private static async Task<Created<FolderResponse>> CreateAsync(
        CreateFolderRequest request,
        [FromServices] IFolderService folders,
        [FromServices] IHttpContextAccessor accessor,
        CancellationToken cancellationToken)
    {
        Guid ownerUserId = ResolveOwnerUserId(accessor.HttpContext);

        Folder folder = await folders
            .CreateAsync(request.ParentFolderId, request.Name, ownerUserId, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Created($"/folders/{folder.Id}", folder.ToResponse());
    }

    private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> RenameAsync(
        Guid id,
        RenameFolderRequest request,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        Folder? folder = await folders
            .RenameAsync(id, request.Name, cancellationToken)
            .ConfigureAwait(false);
        if (folder is null)
        {
            return TypedResults.Problem(
                $"Folder '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.Ok(folder.ToResponse());
    }

    private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> MoveAsync(
        Guid id,
        MoveFolderRequest request,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        try
        {
            Folder? folder = await folders
                .MoveAsync(id, request.NewParentFolderId, cancellationToken)
                .ConfigureAwait(false);
            if (folder is null)
            {
                return TypedResults.Problem(
                    $"Folder '{id}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return TypedResults.Ok(folder.ToResponse());
        }
        catch (InvalidOperationException ex)
        {
            // Cycle, cross-tenant, descendant target, trashed target, or root invariant.
            return TypedResults.Problem(ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
    }

    private static async Task<Results<Ok<FolderResponse>, ProblemHttpResult>> TrashAsync(
        Guid id,
        [FromServices] IFolderService folders,
        CancellationToken cancellationToken)
    {
        Folder? folder = await folders.TrashAsync(id, cancellationToken).ConfigureAwait(false);
        if (folder is null)
        {
            return TypedResults.Problem(
                $"Folder '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound);
        }
        return TypedResults.Ok(folder.ToResponse());
    }

    private static Guid ResolveOwnerUserId(HttpContext? httpContext)
    {
        if (httpContext is null)
        {
            return Guid.Empty;
        }

        // The User aggregate id (ADR-051) is exposed on the principal as the standard
        // ClaimTypes.NameIdentifier ("sub"). Falling back to Guid.Empty here keeps
        // anonymous flows (test harnesses) working; production deployments enforce
        // authentication via .RequireAuthorization() on the route group.
        string? sub = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out Guid id) ? id : Guid.Empty;
    }
}
