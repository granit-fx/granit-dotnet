using Granit.BackgroundJobs;

namespace Granit.Authentication.ApiKeys.BackgroundJobs.Jobs;

/// <summary>
/// Recurring scanner that surfaces API keys approaching expiration. Runs daily at
/// 07:00 UTC — early enough to land in administrators' morning inboxes, late enough
/// to avoid contention with overnight maintenance. The lead-time window
/// (default 14 days) is configurable via
/// <see cref="Options.ApiKeysOptions.ExpirationLeadTimeDays"/>; per-key dedupe
/// (one alert per week) is enforced by <c>ApiKeyEntry.LastExpirationNotifiedAt</c>.
/// </summary>
[RecurringJob("0 7 * * *", "apikeys-expiring-soon")]
public sealed record ExpiringApiKeyScannerJob : IBackgroundJob;
