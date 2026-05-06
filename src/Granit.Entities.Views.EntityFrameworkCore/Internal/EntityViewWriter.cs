using Granit.Authorization;
using Granit.Entities.Views.Domain;
using Granit.Users;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed implementation of <see cref="IEntityViewWriter"/>. Enforces the closed
/// permission set of <see cref="EntityViewPermissions"/> per ADR-047 §6 and persists
/// every aggregate mutation through the <see cref="EntityViewDbContext"/>'s standard
/// audit pipeline (the <c>AuditedEntityInterceptor</c> records who-changed-what and
/// when via <see cref="Granit.Domain.AuditedAggregateRoot"/>).
/// </summary>
internal sealed class EntityViewWriter(
    EntityViewDbContext dbContext,
    IPermissionChecker permissions,
    ICurrentUserService currentUser) : IEntityViewWriter
{
    public async Task<EntityViewDescriptor> CreateAsync(
        EntityViewCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Guid ownerId = RequireUserId();
        await RequirePermissionAsync(EntityViewPermissions.Create, cancellationToken).ConfigureAwait(false);

        var view = EntityView.Create(
            entityName: request.EntityName,
            basedOn: request.BasedOn,
            kind: request.Kind,
            name: request.Name,
            description: request.Description,
            icon: request.Icon,
            state: request.State,
            ownerId: ownerId);

        dbContext.EntityViews.Add(view);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return view.ToDescriptor();
    }

    public async Task<EntityViewDescriptor> UpdateAsync(
        Guid id,
        EntityViewUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        await RequireMutationAuthorityAsync(view, cancellationToken).ConfigureAwait(false);

        view.Rename(request.Name, request.Description, request.Icon);
        view.UpdateState(request.State);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return view.ToDescriptor();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);

        bool isOwner = TryGetUserId() is { } userId && view.OwnerId == userId;
        if (!isOwner)
        {
            await RequirePermissionAsync(EntityViewPermissions.DeleteAny, cancellationToken).ConfigureAwait(false);
        }

        dbContext.EntityViews.Remove(view);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<EntityViewDescriptor> SetPinnedAsync(
        Guid id,
        bool isPinned,
        CancellationToken cancellationToken = default)
    {
        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        await RequirePermissionAsync(EntityViewPermissions.Manage, cancellationToken).ConfigureAwait(false);

        view.SetPinned(isPinned);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return view.ToDescriptor();
    }

    public async Task<EntityViewDescriptor> SetTenantDefaultAsync(
        Guid id,
        bool isDefault,
        CancellationToken cancellationToken = default)
    {
        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        await RequirePermissionAsync(EntityViewPermissions.Manage, cancellationToken).ConfigureAwait(false);

        if (isDefault)
        {
            // Only one tenant-default per (entity, tenant) — clear any existing one.
            List<EntityView> previousDefaults = await dbContext.EntityViews
                .Where(v => v.EntityName == view.EntityName && v.IsDefault && v.Id != view.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (EntityView prev in previousDefaults)
            {
                prev.SetTenantDefault(false);
            }
        }

        view.SetTenantDefault(isDefault);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return view.ToDescriptor();
    }

    public async Task<EntityViewDescriptor> SetPersonalDefaultAsync(
        Guid id,
        bool isPersonalDefault,
        CancellationToken cancellationToken = default)
    {
        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        Guid userId = RequireUserId();

        if (view.OwnerId != userId)
        {
            throw new UnauthorizedAccessException(
                "Only the owner may set a view as their personal default.");
        }

        if (isPersonalDefault)
        {
            // Only one personal-default per (entity, user) — clear any existing one.
            List<EntityView> previousDefaults = await dbContext.EntityViews
                .Where(v => v.EntityName == view.EntityName
                    && v.IsPersonalDefault
                    && v.OwnerId == userId
                    && v.Id != view.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (EntityView prev in previousDefaults)
            {
                prev.SetPersonalDefault(false);
            }
        }

        view.SetPersonalDefault(isPersonalDefault);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return view.ToDescriptor();
    }

    public async Task<EntityViewDescriptor> ShareAsync(
        Guid id,
        EntityViewSharedWith audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        EntityView view = await LoadOrThrowAsync(id, cancellationToken).ConfigureAwait(false);
        await RequirePermissionAsync(EntityViewPermissions.Share, cancellationToken).ConfigureAwait(false);

        view.ShareWith(audience);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return view.ToDescriptor();
    }

    private async Task<EntityView> LoadOrThrowAsync(Guid id, CancellationToken cancellationToken)
    {
        EntityView? view = await dbContext.EntityViews
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return view ?? throw new EntityViewNotFoundException(id);
    }

    private async Task RequireMutationAuthorityAsync(EntityView view, CancellationToken cancellationToken)
    {
        Guid? userId = TryGetUserId();
        bool isOwner = userId is { } id && view.OwnerId == id;

        if (view.Visibility == EntityViewVisibility.Tenant)
        {
            await RequirePermissionAsync(EntityViewPermissions.Manage, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (isOwner)
        {
            return;
        }

        await RequirePermissionAsync(EntityViewPermissions.Manage, cancellationToken).ConfigureAwait(false);
    }

    private async Task RequirePermissionAsync(string permission, CancellationToken cancellationToken)
    {
        if (!await permissions.IsGrantedAsync(permission, cancellationToken).ConfigureAwait(false))
        {
            throw new UnauthorizedAccessException(
                $"The current user does not hold the required permission '{permission}'.");
        }
    }

    private Guid RequireUserId() =>
        TryGetUserId() ?? throw new UnauthorizedAccessException(
            "An authenticated user is required to perform this operation.");

    private Guid? TryGetUserId() =>
        Guid.TryParse(currentUser.UserId, out Guid id) ? id : null;
}
