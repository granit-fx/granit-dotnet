using System.Text.Json;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Internal;

/// <summary>
/// Durable <see cref="IIdentitySecurityStateStore"/> backed by EF Core. Risk verdicts, device trust, the
/// behavioural profile, and session-review decisions survive restarts and are shared across instances. Each
/// facet keeps its own table in <see cref="IdentityDbContext"/>; this class merges what used to be four
/// separate EF stores into one, sharing the context factory and GUID generator.
/// </summary>
internal sealed class EfCoreIdentitySecurityStateStore(
    IDbContextFactory<IdentityDbContext> dbContextFactory,
    IGuidGenerator guidGenerator) : IIdentitySecurityStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    // ── Session risk ──────────────────────────────────────────────────

    public async Task SetSessionRiskAsync(
        string userId, string sessionId, UserSessionRiskVerdict verdict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        string reasonsJson = JsonSerializer.Serialize(verdict.Reasons, JsonOptions);

        // Upsert with one retry: two concurrent assessments of the same (userId, sessionId) both miss the
        // existing row and both insert, tripping the unique index. On that conflict, re-read and update the
        // row the winner created instead of surfacing the DbUpdateException. Provider-agnostic (no ON CONFLICT).
        // The second attempt's own DbUpdateException is not caught (filter is attempt == 0), so it surfaces.
        for (int attempt = 0; attempt <= 1; attempt++)
        {
            await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            UserSessionRiskEntity? existing = await db.UserSessionRisks
                .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is not null)
            {
                existing.Level = verdict.Level;
                existing.ReasonsJson = reasonsJson;
                existing.AssessedAt = verdict.AssessedAt;
            }
            else
            {
                db.UserSessionRisks.Add(new UserSessionRiskEntity
                {
                    Id = guidGenerator.Create(),
                    UserId = userId,
                    SessionId = sessionId,
                    Level = verdict.Level,
                    ReasonsJson = reasonsJson,
                    AssessedAt = verdict.AssessedAt,
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

    public async Task<UserSessionRiskVerdict?> GetSessionRiskAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        UserSessionRiskEntity? entity = await db.UserSessionRisks
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapRisk(entity);
    }

    public async Task<IReadOnlyDictionary<string, UserSessionRiskVerdict>> GetSessionRisksAsync(
        string userId, IReadOnlyCollection<string> sessionIds, CancellationToken cancellationToken = default)
    {
        if (sessionIds.Count == 0)
        {
            return new Dictionary<string, UserSessionRiskVerdict>();
        }

        List<string> ids = [.. sessionIds];

        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UserSessionRiskEntity> rows = await db.UserSessionRisks
            .AsNoTracking()
            .Where(e => e.UserId == userId && ids.Contains(e.SessionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.SessionId, MapRisk);
    }

    // ── Device trust ──────────────────────────────────────────────────

    public async Task SetDeviceTrustAsync(
        string userId, string deviceId, DeviceTrustVerdict verdict, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(verdict);

        // Upsert with one retry: two concurrent "trust this device" writes for the same (userId, deviceId)
        // both miss the existing row and insert, tripping the unique index. On that conflict, re-read and
        // update the row the winner created. Provider-agnostic (no ON CONFLICT). The second attempt's own
        // DbUpdateException is no longer caught (filter is attempt == 0), so it surfaces — bounding the loop.
        for (int attempt = 0; attempt <= 1; attempt++)
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

    public async Task<DeviceTrustVerdict?> GetDeviceTrustAsync(
        string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        DeviceTrustEntity? entity = await db.DeviceTrusts
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.DeviceId == deviceId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapDeviceTrust(entity);
    }

    public async Task<IReadOnlyDictionary<string, DeviceTrustVerdict>> GetDeviceTrustsAsync(
        string userId, IReadOnlyCollection<string> deviceIds, CancellationToken cancellationToken = default)
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

        return rows.ToDictionary(r => r.DeviceId, MapDeviceTrust);
    }

    public async Task RevokeDeviceTrustAsync(
        string userId, string deviceId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await db.DeviceTrusts
            .Where(e => e.UserId == userId && e.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    // ── Behavioural profile ───────────────────────────────────────────

    public async Task<UserBehavioralProfile> GetBehavioralProfileAsync(
        string userId, CancellationToken cancellationToken = default)
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

    public async Task RecordBehavioralObservationAsync(
        string userId,
        string? country,
        string? deviceFamily,
        string? coarseLocation,
        DateTimeOffset observedAt,
        CancellationToken cancellationToken = default)
    {
        List<(BehavioralObservationKind Kind, string Value)> signals = [];
        AddSignal(signals, BehavioralObservationKind.Country, country);
        AddSignal(signals, BehavioralObservationKind.DeviceFamily, deviceFamily);
        AddSignal(signals, BehavioralObservationKind.CoarseLocation, coarseLocation);
        if (signals.Count == 0)
        {
            return;
        }

        // Upsert+increment each signal with one retry: two concurrent observations of the same
        // (userId, kind, value) can both miss the existing row and both insert, tripping the unique index. On
        // that conflict, re-read and increment the winner's row instead of surfacing the DbUpdateException.
        // The second attempt's own DbUpdateException is not caught (filter is attempt == 0), so it surfaces.
        for (int attempt = 0; attempt <= 1; attempt++)
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

    // ── Session review (single-use) ───────────────────────────────────

    public async Task<UserSessionReviewDecision?> GetSessionReviewDecisionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        UserSessionReviewEntity? entity = await db.UserSessionReviews
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
            .ConfigureAwait(false);

        return entity?.Decision;
    }

    public async Task<bool> TryRecordSessionReviewDecisionAsync(
        string userId,
        string sessionId,
        UserSessionReviewDecision decision,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default)
    {
        await using IdentityDbContext db = await dbContextFactory.CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        db.UserSessionReviews.Add(new UserSessionReviewEntity
        {
            Id = guidGenerator.Create(),
            UserId = userId,
            SessionId = sessionId,
            Decision = decision,
            ReviewedAt = reviewedAt,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // A row may already exist (repeat click / scanner prefetch / concurrent winner). Distinguish that
            // "already reviewed" case — return false — from a genuine failure, which we rethrow.
            await using IdentityDbContext check = await dbContextFactory.CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            bool exists = await check.UserSessionReviews
                .AsNoTracking()
                .AnyAsync(e => e.UserId == userId && e.SessionId == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (exists)
            {
                return false;
            }

            throw;
        }
    }

    // ── Mapping helpers ───────────────────────────────────────────────

    private static UserSessionRiskVerdict MapRisk(UserSessionRiskEntity entity) =>
        new(
            entity.Level,
            JsonSerializer.Deserialize<List<string>>(entity.ReasonsJson, JsonOptions) ?? [],
            entity.AssessedAt);

    private static DeviceTrustVerdict MapDeviceTrust(DeviceTrustEntity entity) =>
        new(entity.Level, entity.TrustedAt, entity.TrustedUntil, entity.Reason);

    private static void AddSignal(
        List<(BehavioralObservationKind Kind, string Value)> signals, BehavioralObservationKind kind, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            signals.Add((kind, value));
        }
    }
}
