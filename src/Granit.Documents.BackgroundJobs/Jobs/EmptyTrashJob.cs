using Granit.BackgroundJobs;

namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>
/// F9.2 — recurring promotion of trashed documents whose <c>TrashedAt</c> exceeded
/// <c>GranitDocumentsOptions.TrashRetentionDays</c> to <c>PermanentlyDeleted</c>.
/// Runs nightly at 03:00 UTC, off-peak for most tenants.
/// </summary>
[RecurringJob("0 3 * * *", "documents-empty-trash")]
public sealed record EmptyTrashJob : IBackgroundJob;
