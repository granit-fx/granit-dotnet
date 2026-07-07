namespace Granit.BackgroundJobs;

/// <summary>
/// Well-known header names used by the background jobs infrastructure.
/// </summary>
public static class BackgroundJobHeaders
{
    /// <summary>
    /// Header carrying the admin user identity for manual triggers (ISO 27001 audit).
    /// </summary>
    public const string TriggeredBy = "X-Triggered-By";

    /// <summary>
    /// Header marking an execution as manually triggered via
    /// <see cref="IBackgroundJobWriter.TriggerNowAsync"/>. The scheduling middleware skips
    /// rescheduling for such executions so a manual run never duplicates the recurring chain —
    /// the regular occurrence stays armed and untouched.
    /// </summary>
    public const string ManualTrigger = "X-Manual-Trigger";
}
