using Granit.Notifications;
using Granit.Notifications.Abstractions;

namespace Granit.BackgroundJobs.Notifications.Internal;

/// <summary>
/// Registers background-jobs notification definitions. Operational alerts —
/// non-opt-outable for platform administrators.
/// </summary>
internal sealed class BackgroundJobsNotificationDefinitionProvider : INotificationDefinitionProvider
{
    private const string GroupName = "Operations";

    public void Define(INotificationDefinitionContext context)
    {
        context.Add(new NotificationDefinition(JobsRecurringFailingNotificationType.Instance.Name)
        {
            GroupName = GroupName,
            DisplayName = "Recurring Job Failing",
            Description = "Alerts platform administrators when a recurring background job has failed several consecutive runs.",
            DefaultSeverity = NotificationSeverity.Warning,
            DefaultChannels = [NotificationChannels.Email, NotificationChannels.InApp],
            AllowUserOptOut = false,
        });
    }
}
