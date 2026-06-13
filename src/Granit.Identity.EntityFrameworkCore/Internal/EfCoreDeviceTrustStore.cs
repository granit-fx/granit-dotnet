using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IDeviceTrustStore"/> backed by EF Core. Trust verdicts survive restarts and are shared
/// across instances, so a device marked trusted on one node is honoured everywhere.
/// </summary>
internal sealed class EfCoreDeviceTrustStore(
    IDbContextFactory<IdentityDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IDeviceTrustStore
{
    public async Task SetAsync(
        string userId,
        string deviceId,
        DeviceTrustVerdict verdict,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        // Upsert with one retry: two concurrent "trust this device" writes for the same (userId, deviceId)
        // both miss the existing row and insert, tripping the unique index. On that conflict, re-read and
        // update the row the winner created. Provider-agnostic (no ON CONFLICT).
        for (int attempt = 0; ; attempt++)
        {
            await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            DeviceTrustEntity? existing = await db.DeviceTrusts
                .FirstOrDefaultAsync(e => e.UserId == userId && e.DeviceId == deviceId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Level = verdict.Level;
                existing.TrustedAt = verdict.TrustedAt;
                existing.TrustedUntil = verdict.TrustedUntil;
                existing.Reason = verdict.Reason;
            }
            else
            {
                db.DeviceTrusts.Add(new DeviceTrustEntity
                {
                    Id = guidGenerator.Create(),
                    UserId = userId,
                    DeviceId = deviceId,
                    Level = verdict.Level,
                    TrustedAt = verdict.TrustedAt,
                    TrustedUntil = verdict.TrustedUntil,
                    Reason = verdict.Reason,
                });
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                // Lost an insert race; loop once to re-read and update the winner's row.
            }
        }
    }

    public async Task<DeviceTrustVerdict?> GetAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DeviceTrustEntity? entity = await db.DeviceTrusts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.DeviceId == deviceId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetManyAsync(
        string userId,
        IReadOnlyCollection<string> deviceIds,
        CancellationToken cancellationToken = default)
    {
        if (deviceIds.Count == 0)
        {
            return new Dictionary<string, DeviceTrustVerdict>();
        }

        List<string> ids = [.. deviceIds];

        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<DeviceTrustEntity> rows = await db.DeviceTrusts
            .AsNoTracking()
            .Where(e => e.UserId == userId && ids.Contains(e.DeviceId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.DeviceId, Map);
    }

    public async Task RevokeAsync(
        string userId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await db.DeviceTrusts
            .Where(e => e.UserId == userId && e.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static DeviceTrustVerdict Map(DeviceTrustEntity entity) =>
        new(entity.Level, entity.TrustedAt, entity.TrustedUntil, entity.Reason);
}
