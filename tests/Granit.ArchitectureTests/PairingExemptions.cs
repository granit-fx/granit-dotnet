namespace Granit.ArchitectureTests;

/// <summary>
/// Canonical list of entities exempted from the Query ↔ Export pairing rule
/// (ADR-020). Consumed by <c>QueryExportPairingTests</c> and
/// <c>PermissionLocalizationCompletenessTests</c>.
/// </summary>
/// <remarks>
/// Entries are <c>[INFRA]</c> only — pure infrastructure / audit / config /
/// internal cache entities that surface in neither admin grids nor business
/// KPIs. Adding a new entry requires a one-line justification (inline comment).
/// The previous metric-backlog category moved out with the analytics contracts.
/// </remarks>
internal static class PairingExemptions
{
    /// <summary>
    /// Permanent <c>[INFRA]</c> exemptions — entities that surface in neither
    /// admin grids nor business KPIs (audit logs, RBAC config, internal caches,
    /// platform settings, transient job records).
    /// </summary>
    public static readonly HashSet<string> Infrastructure = new(StringComparer.Ordinal)
    {
        "Granit.AI.AIUsageRecord",                                                        // [INFRA] AI cost / audit log
        "Granit.Auditing.Domain.AuditEntityChange",                                       // [INFRA] audit log
        "Granit.Auditing.Domain.AuditEntry",                                              // [INFRA] audit log
        "Granit.Authorization.Domain.PermissionGrant",                                    // [INFRA] RBAC config
        "Granit.Authorization.Domain.RoleMetadata",                                       // [INFRA] RBAC config
        "Granit.BackgroundJobs.Domain.BackgroundJobDefinition",                           // [INFRA] job config
        "Granit.DataExchange.Export.Domain.ExportJob",                                    // [INFRA] transient export job
        "Granit.DataExchange.Import.Domain.ImportJob",                                    // [INFRA] transient import job
        "Granit.Identity.Federated.Domain.FederatedIdentity",                                // [INFRA] internal user cache
        "Granit.Identity.Local.Domain.GranitRole",                                        // [INFRA] RBAC config
        "Granit.Identity.Local.Domain.GranitUserGroup",                                   // [INFRA] RBAC config
        "Granit.Localization.Domain.LocalizationOverride",                                // [INFRA] localization config
        "Granit.MultiTenancy.Domain.Tenant",                                              // [INFRA] platform-admin entity
        "Granit.Notifications.Domain.NotificationPreference",                             // [INFRA] user preference config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication",              // [INFRA] OAuth client config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope",                    // [INFRA] OAuth scope config
        "Granit.Presence.Domain.UserPresence",                                            // [INFRA] runtime presence state, not admin-grid material
        "Granit.Scheduling.Domain.ScheduledAction",                                       // [INFRA] scheduling state
        "Granit.Settings.Domain.SettingRecord",                                           // [INFRA] settings config
        "Granit.Timeline.Domain.TimelineEntry",                                           // [INFRA] audit log
        "Granit.Workflow.Domain.WorkflowTransitionRecord",                                // [INFRA] workflow audit
    };
}
