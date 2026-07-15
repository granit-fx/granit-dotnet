using Granit.BackgroundJobs;

namespace Granit.OpenIddict.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that publishes the <c>UserRegisteredEto</c> for any account whose registration
/// event has not yet been dispatched.
/// </summary>
/// <remarks>
/// Runs every five minutes. The registration flow publishes the event inline for immediate side
/// effects but records the intent durably (the OpenIddict DbContext is not enrolled in the Wolverine
/// outbox); this job reconciles the durable stragglers into published events at-least-once when the
/// inline publish was lost to a crash.
/// </remarks>
[RecurringJob("*/5 * * * *", "openiddict-registration-eto-dispatch")]
public sealed record OpenIddictRegistrationEtoDispatchJob : IBackgroundJob;
