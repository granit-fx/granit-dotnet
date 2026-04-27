using Granit.Authentication.ApiKeys.Domain;
using Granit.Authentication.ApiKeys.Events;
using Granit.Authentication.ApiKeys.Options;
using Granit.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.ApiKeys.BackgroundJobs.Services;

/// <summary>
/// Scans active API keys whose <see cref="ApiKeyEntry.ExpiresAt"/> falls within the
/// configured lead time and emits an <see cref="ApiKeyExpiringSoonEto"/> for each
/// match — once per week per key (dedupe via
/// <see cref="ApiKeyEntry.LastExpirationNotifiedAt"/>). Idempotent and safe to run
/// at any cadence: extra ticks find nothing new and exit early.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a scanner instead of a saga.</b> An API key's lifecycle is mostly silent —
/// the only domain event between issuance and expiration is <c>ApiKeyUsedEto</c> per
/// request. Scheduling a "wake up at <c>ExpiresAt - 14 days</c>" timer per key would
/// be possible but heavier than a daily query that scans only non-revoked keys with
/// <c>ExpiresAt</c> set. The composite index
/// <c>ix_*_api_keys_expiring_scan</c> keeps this O(matching keys).
/// </para>
/// <para>
/// <b>Order of operations.</b> The Eto is published <em>before</em> the timestamp
/// stamp — Wolverine's outbox stages the message in the same scope so a failure
/// stamping <c>LastExpirationNotifiedAt</c> rolls back the publish. Reverse order
/// would risk silently losing a notification.
/// </para>
/// <para>
/// <b>Secret hygiene.</b> The Eto carries only public-safe metadata
/// (<see cref="ApiKeyExpiringSoonEto.KeyId"/>,
/// <see cref="ApiKeyExpiringSoonEto.KeyName"/>,
/// <see cref="ApiKeyExpiringSoonEto.KeyType"/>,
/// <see cref="ApiKeyExpiringSoonEto.ExpiresAt"/>,
/// <see cref="ApiKeyExpiringSoonEto.TenantId"/>). The hash, raw value and prefix are
/// never read here.
/// </para>
/// </remarks>
public sealed partial class ExpiringApiKeyScannerService(
    IApiKeyAdminStore adminStore,
    IDistributedEventBus eventBus,
    TimeProvider timeProvider,
    IOptions<ApiKeysOptions> options,
    ILogger<ExpiringApiKeyScannerService> logger)
{
    /// <summary>Dedupe window — at most one expiring-soon alert per key per week.</summary>
    internal static readonly TimeSpan DedupeWindow = TimeSpan.FromDays(7);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        ApiKeysOptions opts = options.Value;
        DateTimeOffset leadTimeWindowEnd = now.AddDays(opts.ExpirationLeadTimeDays);
        DateTimeOffset dedupeBefore = now - DedupeWindow;

        IReadOnlyList<ApiKeyEntry> expiringSoon = await adminStore
            .ListExpiringSoonAsync(now, leadTimeWindowEnd, dedupeBefore, cancellationToken)
            .ConfigureAwait(false);

        if (expiringSoon.Count == 0)
        {
            return;
        }

        Log.ExpiringKeysFound(logger, expiringSoon.Count, opts.ExpirationLeadTimeDays);

        foreach (ApiKeyEntry entry in expiringSoon)
        {
            // ExpiresAt is non-null by query construction, but the entity allows null
            // and the compiler can't see the join — defensive guard.
            if (entry.ExpiresAt is null)
            {
                continue;
            }

            await eventBus.PublishAsync(
                new ApiKeyExpiringSoonEto(
                    entry.Id,
                    entry.Name,
                    entry.Type,
                    entry.ExpiresAt.Value,
                    entry.TenantId),
                cancellationToken).ConfigureAwait(false);

            entry.MarkExpirationNotified(now);
            await adminStore.SaveAsync(entry, cancellationToken).ConfigureAwait(false);

            Log.ExpiringKeyNotified(logger, entry.Id, entry.ExpiresAt.Value);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Expiring-soon scan found {Count} API key(s) expiring within {LeadTimeDays} day(s).")]
        public static partial void ExpiringKeysFound(ILogger logger, int count, int leadTimeDays);

        [LoggerMessage(Level = LogLevel.Information, Message = "Emitted ApiKeyExpiringSoonEto for key {KeyId} (expires {ExpiresAt}).")]
        public static partial void ExpiringKeyNotified(ILogger logger, Guid keyId, DateTimeOffset expiresAt);
    }
}
