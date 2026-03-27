using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Microsoft.EntityFrameworkCore;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// <see cref="ILocalIdentityGroupStore"/> implementation backed by <see cref="OpenIddictDbContext"/>.
/// </summary>
internal sealed class OpenIddictGroupStore(
    IDbContextFactory<OpenIddictDbContext> _dbFactory) : ILocalIdentityGroupStore
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<GranitUserGroup> groups = await db.UserGroups.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return groups.Select(g => new IdentityGroup(g.Id.ToString(), g.Name, null, [])).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));

        await using OpenIddictDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<Guid> groupIds = await db.UserGroupMembers.AsNoTracking()
            .Where(m => m.UserId == userGuid)
            .Select(m => m.GroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<GranitUserGroup> groups = await db.UserGroups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return groups.Select(g => new IdentityGroup(g.Id.ToString(), g.Name, null, [])).ToList();
    }

    /// <inheritdoc/>
    public async Task AddUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));
        Guid groupGuid = ParseGuid(groupId, nameof(groupId));

        await using OpenIddictDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        db.UserGroupMembers.Add(new GranitUserGroupMember
        {
            GroupId = groupGuid,
            UserId = userGuid,
        });
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Guid ParseGuid(string value, string parameterName) =>
        Guid.TryParse(value, out Guid result)
            ? result
            : throw new ArgumentException($"'{value}' is not a valid GUID.", parameterName);

    /// <inheritdoc/>
    public async Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default)
    {
        Guid userGuid = ParseGuid(userId, nameof(userId));
        Guid groupGuid = ParseGuid(groupId, nameof(groupId));

        await using OpenIddictDbContext db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        GranitUserGroupMember? member = await db.UserGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupGuid && m.UserId == userGuid,
                cancellationToken).ConfigureAwait(false);

        if (member is not null)
        {
            db.UserGroupMembers.Remove(member);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
