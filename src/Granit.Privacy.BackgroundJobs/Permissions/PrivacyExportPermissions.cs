namespace Granit.Privacy.BackgroundJobs.Permissions;

/// <summary>
/// Permission constants for the privacy export job. Granted to data subjects so they
/// can request their own export (GDPR Art. 15 / Art. 20 — a personal right, not an
/// application usage right). Admin-on-behalf-of flows ship in v1.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why split.</b> <see cref="Exports.Execute"/> is the self-service permission a
/// data subject needs to dispatch their own export — granted by default to every
/// authenticated user. <see cref="Exports.OnBehalfOf"/> is the admin DSR permission
/// required to dispatch an export targeting a <i>different</i> subject; reserved for
/// a v1.1 follow-up. Splitting now keeps the seed simple while leaving the future
/// admin endpoint a stable contract to point at.
/// </para>
/// <para>
/// <b>Enforcement.</b> The HTTP endpoint that dispatches the job enforces the
/// permission against the caller's principal; the background-job handler trusts the
/// envelope.
/// </para>
/// </remarks>
public static class PrivacyExportPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "Privacy";

    /// <summary>Permissions for the personal-data export flow.</summary>
    public static class Exports
    {
        /// <summary>
        /// Self-service permission: grants the caller the ability to request their own
        /// personal-data export. GDPR Art. 15/20 frames data portability as a personal
        /// right, so the default seed grants this to every authenticated user.
        /// </summary>
        public const string Execute = "Privacy.Exports.Execute";

        /// <summary>
        /// Admin DSR permission: grants the caller the ability to dispatch an export
        /// for a different data subject. Restrict to data-protection officers and
        /// support operators with documented legal basis.
        /// </summary>
        public const string OnBehalfOf = "Privacy.Exports.OnBehalfOf";
    }
}
