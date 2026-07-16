using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Local.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="ILocalIdentityGroupStore"/> implementation backed by <see cref="IdentityLocalDbContext"/>.
/// </summary>
internal sealed class IdentityLocalGroupStore(
    IDbContextFactory<IdentityLocalDbContext> dbFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<GranitUserGroup, IdentityLocalDbContext>(dbFactory, currentTenant), ILocalIdentityGroupStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<GranitUserGroup> groups = await ReadAsync(
            db => db.UserGroups.AsNoTracking().ToListAsync(cancellationToken),
            cancellationToken).ConfigureAwait(false);

        return groups.Select(g => new IdentityGroup(g.Id.ToString(), g.Name, null, [])).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));

        IReadOnlyList<GranitUserGroup> groups = await ReadAsync(async db =>
        {
            List<Guid> groupIds = await db.UserGroupMembers.AsNoTracking()
                .Where(m => m.UserId == userGuid)
                .Select(m => m.GroupId)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            return (IReadOnlyList<GranitUserGroup>)await db.UserGroups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToListAsync(cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);

        return groups.Select(g => new IdentityGroup(g.Id.ToString(), g.Name, null, [])).ToList();
    }

    /// <inheritdoc/>
    public Task AddUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));
        Guid groupGuid = ParseGuid(groupId, nameof(groupId));

        return WriteAsync(db =>
        {
            db.UserGroupMembers.Add(new GranitUserGroupMember
            {
                GroupId = groupGuid,
                UserId = userGuid,
            });

            return Task.CompletedTask;
        }, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));
        Guid groupGuid = ParseGuid(groupId, nameof(groupId));

        return WriteAsync(async db =>
        {
            GranitUserGroupMember? member = await db.UserGroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == groupGuid && m.UserId == userGuid,
                    cancellationToken).ConfigureAwait(false);

            if (member is not null)
            {
                db.UserGroupMembers.Remove(member);
            }
        }, cancellationToken);
    }

    private static Guid ParseGuid(string value, string parameterName) =>
        Guid.TryParse(value, out Guid result)
            ? result
            : throw new ArgumentException($"'{value}' is not a valid GUID.", parameterName);
}
