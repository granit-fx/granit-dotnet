using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IUserBehavioralProfileStore"/> backed by EF Core. The profile survives restarts and is
/// shared across instances, so a habitual location/device stays recognised between visits instead of resetting.
/// </summary>
internal sealed class EfCoreUserBehavioralProfileStore(
    IDbContextFactory<IdentityDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IUserBehavioralProfileStore
{
    public async Task<UserBehavioralProfile> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UserBehavioralProfileEntity> rows = await db.UserBehavioralProfiles
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Count == 0
            ? UserBehavioralProfile.Empty
            : new UserBehavioralProfile(
                [.. rows.Select(r => new BehavioralObservation(r.Kind, r.Value, r.Count, r.FirstSeenAt, r.LastSeenAt))]);
    }

    public async Task RecordObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        List<(BehavioralObservationKind Kind, string Value)> signals = [];
        Add(signals, BehavioralObservationKind.Country, country);
        Add(signals, BehavioralObservationKind.DeviceFamily, deviceFamily);
        Add(signals, BehavioralObservationKind.CoarseLocation, coarseLocation);
        if (signals.Count == 0)
        {
            return;
        }

        // Upsert+increment each signal with one retry: two concurrent observations of the same
        // (userId, kind, value) can both miss the existing row and both insert, tripping the unique index. On
        // that conflict, re-read and increment the winner's row instead of surfacing the DbUpdateException.
        for (int attempt = 0; ; attempt++)
        {
            await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach ((BehavioralObservationKind kind, string value) in signals)
            {
                UserBehavioralProfileEntity? existing = await db.UserBehavioralProfiles
                    .FirstOrDefaultAsync(
                        e => e.UserId == userId && e.Kind == kind && e.Value == value, cancellationToken)
                    .ConfigureAwait(false);

                if (existing is not null)
                {
                    existing.Count++;
                    existing.LastSeenAt = observedAt;
                }
                else
                {
                    db.UserBehavioralProfiles.Add(new UserBehavioralProfileEntity
                    {
                        Id = guidGenerator.Create(),
                        UserId = userId,
                        Kind = kind,
                        Value = value,
                        Count = 1,
                        FirstSeenAt = observedAt,
                        LastSeenAt = observedAt,
                    });
                }
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Lost an insert race; loop once to re-read and increment the winner's row.
            }
        }
    }

    private static void Add(
        List<(BehavioralObservationKind Kind, string Value)> signals, BehavioralObservationKind kind, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            signals.Add((kind, value));
        }
    }
}
