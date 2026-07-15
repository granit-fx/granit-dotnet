using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.Services;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IUserSessionActivityStore"/> over
/// <see cref="GranitOpenIddictToken"/> — the durable last-activity lives on the valid refresh token
/// that represents the session.
/// </summary>
internal sealed class EfUserSessionActivityStore(
    IDbContextFactory<OpenIddictDbContext> dbFactory) : IUserSessionActivityStore
{
    /// <inheritdoc/>
    public async Task TouchAsync(
        Guid authorizationId,
        DateTimeOffset lastActivityAt,
        TimeSpan debounceWindow,
        CancellationToken cancellationToken = default)
    {
        DateTimeOffset staleBefore = lastActivityAt - debounceWindow;

        await using OpenIddictDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Debounce in SQL: only write when the stored activity is null or older than the window, so a
        // heartbeat on every request touches the row at most once per window.
        await db.Set<GranitOpenIddictToken>()
            .Where(t => EF.Property<Guid?>(t, "AuthorizationId") == authorizationId
                && t.Type == OpenIddictConstants.TokenTypeHints.RefreshToken
                && t.Status == OpenIddictConstants.Statuses.Valid
                && (t.LastActivityAt == null || t.LastActivityAt < staleBefore))
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.LastActivityAt, lastActivityAt),
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DateTimeOffset>> GetActivitiesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<ActivityRow> rows = await db.Set<GranitOpenIddictToken>()
            .AsNoTracking()
            .Where(t => t.Subject == userId
                && t.Type == OpenIddictConstants.TokenTypeHints.RefreshToken
                && t.Status == OpenIddictConstants.Statuses.Valid
                && t.LastActivityAt != null)
            .Select(t => new ActivityRow(t.Id, t.LastActivityAt!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Session id is the refresh token id as string (what GetIdAsync exposes to the provider).
        return rows.ToDictionary(r => r.SessionId.ToString(), r => r.LastActivityAt);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetIdleRefreshTokenIdsAsync(
        DateTimeOffset idleSince, int max, CancellationToken cancellationToken = default)
    {
        await using OpenIddictDbContext db = await dbFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<Guid> ids = await db.Set<GranitOpenIddictToken>()
            .AsNoTracking()
            .Where(t => t.Type == OpenIddictConstants.TokenTypeHints.RefreshToken
                && t.Status == OpenIddictConstants.Statuses.Valid
                && (t.LastActivityAt ?? t.CreationDate) < idleSince)
            .OrderBy(t => t.LastActivityAt ?? t.CreationDate)
            .Take(max)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ids.ConvertAll(id => id.ToString());
    }

    private readonly record struct ActivityRow(Guid SessionId, DateTimeOffset LastActivityAt);
}
