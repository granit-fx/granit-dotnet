using Granit.Identity.Local.Domain;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// User lookup service that queries <see cref="UserManager{TUser}"/> directly,
/// bypassing any redundant cache layer.
/// </summary>
internal sealed class AspNetIdentityUserLookupService(
    UserManager<LocalIdentity> _userManager) : IUserLookupService
{
    /// <inheritdoc/>
    public async Task<IIdentityUser?> FindByIdAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        LocalIdentity? user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> FindByIdsAsync(
        IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var guidIds = userIds.Select(Guid.Parse).ToList();
        List<LocalIdentity> users = await _userManager.Users.AsNoTracking()
            .Where(u => guidIds.Contains(u.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return users.Cast<IIdentityUser>().ToList();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<IIdentityUser>> SearchAsync(
        string searchTerm, int page = 1, int pageSize = QueryEngineDefaults.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        IQueryable<LocalIdentity> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(searchTerm)) ||
                (u.Email != null && u.Email.Contains(searchTerm)) ||
                (u.FirstName != null && u.FirstName.Contains(searchTerm)) ||
                (u.LastName != null && u.LastName.Contains(searchTerm)));
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        int skip = (page - 1) * pageSize;
        List<LocalIdentity> items = await query
            .OrderBy(u => u.UserName)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        bool hasMore = (skip + items.Count) < totalCount;
        return new PagedResult<IIdentityUser>(
            items.Cast<IIdentityUser>().ToList(),
            totalCount,
            hasMore,
            null);
    }

    /// <inheritdoc/>
    public Task<IIdentityUser?> RefreshByIdAsync(
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

}
