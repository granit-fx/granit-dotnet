using Granit.Activities.BackgroundJobs.Services;

namespace Granit.Activities.BackgroundJobs.Jobs;

/// <summary>
/// Handles <see cref="SendRemindersJob"/> by delegating to
/// <see cref="SendRemindersScanService"/>.
/// </summary>
public class SendRemindersHandler
{
    public static Task HandleAsync(
        SendRemindersJob _,
        SendRemindersScanService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
