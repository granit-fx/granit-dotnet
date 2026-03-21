using Granit.Identity;
using Granit.OpenIddict.Entities;
using Granit.Querying;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GranitIdentityUser = Granit.Identity.Models.IdentityUser;

namespace Granit.Identity.OpenIddict.Internal;

/// <summary>
/// User lookup service that queries <see cref="UserManager{TUser}"/> directly,
/// bypassing any redundant cache layer.
/// </summary>
internal sealed class AspNetIdentityUserLookupService(
    UserManager<GranitUser> _userManager,
    IClock _clock) : IUserLookupService
{
    /// <inheritdoc/>
    public async Task<GranitIdentityUser?> FindByIdAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user is null ? null : MapToIdentityUser(user);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GranitIdentityUser>> FindByIdsAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var guidIds = userIds.Select(Guid.Parse).ToList();
        List<GranitUser> users = await _userManager.Users.AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return users.Select(MapToIdentityUser).ToList();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<GranitIdentityUser>> SearchAsync(
        string search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<GranitUser> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.FirstName != null && u.FirstName.Contains(search)) ||
                (u.LastName != null && u.LastName.Contains(search)));
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        List<GranitUser> items = await query
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        bool hasMore = ((page * pageSize) + items.Count) < totalCount;
        return new PagedResult<GranitIdentityUser>(
            items.Select(MapToIdentityUser).ToList(),
            totalCount,
            hasMore,
            null);
    }

    /// <inheritdoc/>
    public Task<GranitIdentityUser?> RefreshByIdAsync(
        string userId, CancellationToken cancellationToken = default) =>
        FindByIdAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<int> RefreshAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    /// <inheritdoc/>
    public Task<int> RefreshStaleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    /// <inheritdoc/>
    public Task DeleteByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task PseudonymizeByIdAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    private GranitIdentityUser MapToIdentityUser(GranitUser user) =>
        new(
            user.Id.ToString(),
            user.UserName,
            user.Email,
            user.FirstName,
            user.LastName,
            user.LockoutEnd is null || user.LockoutEnd <= _clock.Now);
}
