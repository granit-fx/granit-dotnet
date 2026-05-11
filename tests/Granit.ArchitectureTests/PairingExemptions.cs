namespace Granit.ArchitectureTests;

/// <summary>
/// Canonical list of entities exempted from the cross-primitive pairing rules
/// — Query ↔ Export (ADR-020) and Query ↔ Metric (EPIC #1366 / story #1396).
/// </summary>
/// <remarks>
/// <para>
/// Two categories live here:
/// </para>
/// <list type="bullet">
///   <item>
///     <c>[INFRA]</c> — pure infrastructure / audit / config / internal cache
///     entities. They surface in neither admin grids nor business KPIs and
///     are exempted permanently from <see cref="Infrastructure"/>.
///     Both <c>QueryMetricPairingTests</c> and <c>QueryExportPairingTests</c>
///     consume this single list so additions stay in lockstep.
///   </item>
///   <item>
///     <c>[BACKLOG]</c> — admin-visible entities that should ship at least one
///     <c>MetricDefinition</c> but haven't yet. They live in
///     <c>QueryMetricPairingTests</c> only — backlog is metric-specific. Once
///     a module ships its first <c>MetricDefinition</c> for the entity, the
///     entry is removed and the rule starts enforcing.
///   </item>
/// </list>
/// <para>
/// Adding a new <c>[INFRA]</c> entry MUST come with a one-line justification
/// (inline comment) and must apply to <i>both</i> pairings. If an entity needs
/// only a metric exemption (e.g. it has a real export but no useful KPI), put
/// it in the local <c>QueryMetricPairingTests</c> backlog list, not here.
/// </para>
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
        "Granit.Documents.AssetMetadata.Domain.DocumentAssetMetadata",                    // [INFRA] derived metadata row — no admin grid / business KPI surface, populated by F17 background extractors
        "Granit.Documents.Renditions.Domain.DocumentRendition",                           // [INFRA] derived rendition row, surfaced via the documents view, no admin grid / KPI of its own
        "Granit.Identity.Federated.Domain.FederatedIdentity",                                // [INFRA] internal user cache
        "Granit.Identity.Local.Domain.GranitRole",                                        // [INFRA] RBAC config
        "Granit.Identity.Local.Domain.GranitUserGroup",                                   // [INFRA] RBAC config
        "Granit.Localization.Domain.LocalizationOverride",                                // [INFRA] localization config
        "Granit.Metering.Domain.MeterDefinition",                                         // [INFRA] metering config
        "Granit.MultiTenancy.Domain.Tenant",                                              // [INFRA] platform-admin entity
        "Granit.Notifications.Domain.NotificationPreference",                             // [INFRA] user preference config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication",              // [INFRA] OAuth client config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope",                    // [INFRA] OAuth scope config
        "Granit.Parties.EntityFrameworkCore.Entities.PartyDuplicateCandidate",            // [INFRA] deduplication queue
        "Granit.ReferenceData.Domain.DynamicReferenceDataEntity",                         // [INFRA] reference-data config
        "Granit.Scheduling.Domain.ScheduledAction",                                       // [INFRA] scheduling state
        "Granit.Settings.Domain.SettingRecord",                                           // [INFRA] settings config
        "Granit.Tax.Domain.TaxRateOverride",                                              // [INFRA] tax config
        "Granit.Tax.TaxRateEntry",                                                        // [INFRA] tax config
        "Granit.Timeline.Domain.TimelineEntry",                                           // [INFRA] audit log
        "Granit.Workflow.Domain.WorkflowTransitionRecord",                                // [INFRA] workflow audit
    };
}
