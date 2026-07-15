using Granit.BackgroundJobs;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that publishes the GDPR Art. 17 erasure event (<c>AccountDeletedEto</c>) for any
/// soft-deleted user whose event has not yet been dispatched.
/// </summary>
/// <remarks>
/// Runs every five minutes. The account-deletion service records the soft-delete durably but does
/// not publish inline (the OpenIddict DbContext is not enrolled in the Wolverine outbox); this job
/// reconciles the durable soft-delete rows into published events at-least-once.
/// </remarks>
[RecurringJob("*/5 * * * *", "openiddict-account-deletion-eto-dispatch")]
public sealed record OpenIddictAccountDeletionEtoDispatchJob : IBackgroundJob;
