using Granit.Entities.Views.Domain;
using Granit.Users;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed implementation of <see cref="IEntityViewReader"/>. Filters in two
/// stages — a coarse SQL filter (<c>EntityName</c>, visibility) followed by an
/// in-memory refinement for <see cref="EntityViewVisibility.Shared"/> audience
/// matching. Tenant + soft-delete filters are applied transparently by the
/// <see cref="EntityViewDbContext"/>'s <c>ApplyGranitConventions</c> wiring.
/// </summary>
internal sealed class EntityViewReader(
    EntityViewDbContext dbContext,
    ICurrentUserService currentUser) : IEntityViewReader
{
    public async Task<IReadOnlyList<EntityViewDescriptor>> ListAsync(
        string entityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        Guid? userId = TryParseUserId(currentUser.UserId);
        IReadOnlyList<string> userRoles = currentUser.GetRoles();

        // Coarse SQL filter — pull every Personal-owned-by-user, every Shared, and
        // every Tenant view for the entity. Audience matching for Shared happens in
        // memory below; the index on (TenantId, EntityName) keeps this efficient.
        List<EntityView> candidates = await dbContext.EntityViews
            .AsNoTracking()
            .Where(v => v.EntityName == entityName)
            .Where(v => v.Visibility != EntityViewVisibility.Personal || v.OwnerId == userId)
            .OrderBy(v => v.SortOrder)
            .ThenBy(v => v.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. candidates
            .Where(v => IsAccessibleToCurrentUser(v, userId, userRoles))
            .Select(EntityViewMapping.ToDescriptor)];
    }

    public async Task<EntityViewDescriptor?> GetAsync(
        string entityName,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        EntityView? view = await dbContext.EntityViews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.Id == id && v.EntityName == entityName,
                cancellationToken)
            .ConfigureAwait(false);

        if (view is null)
        {
            return null;
        }

        Guid? userId = TryParseUserId(currentUser.UserId);
        IReadOnlyList<string> userRoles = currentUser.GetRoles();

        return IsAccessibleToCurrentUser(view, userId, userRoles)
            ? view.ToDescriptor()
            : null;
    }

    public async Task<EntityViewDescriptor?> GetDefaultViewAsync(
        string entityName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityName);

        Guid? userId = TryParseUserId(currentUser.UserId);

        // Precedence per ADR-047 §4: IsPersonalDefault > IsDefault > compiled fallback.
        EntityView? personalDefault = userId is null
            ? null
            : await dbContext.EntityViews
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    v => v.EntityName == entityName
                        && v.IsPersonalDefault
                        && v.OwnerId == userId,
                    cancellationToken)
                .ConfigureAwait(false);

        if (personalDefault is not null)
        {
            return personalDefault.ToDescriptor();
        }

        EntityView? tenantDefault = await dbContext.EntityViews
            .AsNoTracking()
            .FirstOrDefaultAsync(
                v => v.EntityName == entityName && v.IsDefault,
                cancellationToken)
            .ConfigureAwait(false);

        return tenantDefault?.ToDescriptor();
    }

    private static bool IsAccessibleToCurrentUser(
        EntityView view,
        Guid? userId,
        IReadOnlyList<string> userRoles) =>
        view.Visibility switch
        {
            EntityViewVisibility.Personal => view.OwnerId == userId,
            EntityViewVisibility.Tenant => true,
            EntityViewVisibility.Shared => MatchesSharedAudience(view.SharedWith, userId, userRoles),
            _ => false,
        };

    private static bool MatchesSharedAudience(
        EntityViewSharedWith? audience,
        Guid? userId,
        IReadOnlyList<string> userRoles)
    {
        if (audience is null)
        {
            return false;
        }

        if (userId is { } id && audience.Users.Contains(id))
        {
            return true;
        }

        return userRoles.Any(role => audience.Roles.Contains(role));
    }

    private static Guid? TryParseUserId(string? raw) =>
        Guid.TryParse(raw, out Guid id) ? id : null;
}
