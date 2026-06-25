namespace Granit.ArchitectureTests;

/// <summary>
/// Columns exempt from the data-lookup coverage rule (<c>QueryDefinitionLookupRules</c>): a
/// filterable <c>*Id</c> column that does not (yet) declare a typeahead picker. Keyed
/// <c>"{EntityFullName}.{PropertyName}"</c>, each with an inline justification.
/// </summary>
/// <remarks>
/// Two kinds of entry:
/// <list type="bullet">
/// <item><b>[PERMANENT]</b> — legitimately no lookup: polymorphic references (<c>EntityId</c> paired
/// with <c>EntityType</c>), opaque/external identifiers (correlation ids, OAuth client ids, external
/// IdP ids), or audit/infra logs that are never an interactive admin grid.</item>
/// <item><b>[BACKLOG]</b> — a real foreign key to an entity that <i>should</i> render a picker; wire
/// <c>.Lookup("&lt;source&gt;")</c> once that lookup source is registered, then remove the entry.</item>
/// </list>
/// <c>TenantId</c> is exempted as a category: it is a cross-cutting multi-tenancy scoping column and
/// the framework ships no tenant lookup source; a host running multi-tenant admin may wire one.
/// <para>
/// Blind spot: definitions without a parameterless constructor (DI-injected) are skipped by the rule
/// and never surface as violations — do not assume such a definition's columns are covered here.
/// </para>
/// </remarks>
internal static class LookupExemptions
{
    public static readonly HashSet<string> Columns = new(StringComparer.Ordinal)
    {
        // ── [PERMANENT] polymorphic references (carry a companion *Type discriminator) ──
        "Granit.Auditing.Domain.AuditEntityChange.EntityId",                     // [PERMANENT] polymorphic (EntityType)
        "Granit.Timeline.Domain.TimelineEntry.EntityId",                         // [PERMANENT] polymorphic (EntityType)
        "Granit.Workflow.Domain.WorkflowTransitionRecord.EntityId",             // [PERMANENT] polymorphic (EntityType)

        // ── [PERMANENT] opaque / external / correlation identifiers ──
        "Granit.Auditing.Domain.AuditEntry.CorrelationId",                       // [PERMANENT] opaque correlation id
        "Granit.Scheduling.Domain.ScheduledAction.CorrelationId",               // [PERMANENT] opaque correlation id
        "Granit.Identity.Federated.Domain.FederatedIdentity.ExternalUserId",     // [PERMANENT] external IdP subject (opaque)
        "Granit.Authorization.Domain.RoleMetadata.ClientId",                     // [PERMANENT] external OAuth client id
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication.ClientId", // [PERMANENT] external OAuth client id
        "Granit.Privacy.LegalAgreements.Domain.LegalDocument.DocumentId",        // [PERMANENT] opaque business slug (e.g. "privacy-policy"); not an FK

        // ── [PERMANENT] audit / infra logs — not interactive admin grids ──
        "Granit.AI.AIUsageRecord.ConversationId",                                // [PERMANENT] AI cost/audit log; owner-private chat ref
        "Granit.Auditing.Domain.AuditEntityChange.AuditEntryId",                 // [PERMANENT] internal audit-log FK
        "Granit.Auditing.Domain.AuditEntry.UserId",                              // [PERMANENT] actor on the audit log (ISO 27001 A.12.4); not a picker
        "Granit.Webhooks.Domain.WebhookDeliveryAttempt.SubscriptionId",          // [PERMANENT] delivery-log FK (infra)

        // ── [PERMANENT] TenantId — cross-cutting tenant scoping; no framework tenant lookup shipped ──
        "Granit.AI.Prompts.Domain.PromptCategory.TenantId",                      // [PERMANENT] tenant scope
        "Granit.AI.Prompts.Domain.PromptTemplate.TenantId",                      // [PERMANENT] tenant scope
        "Granit.Authentication.ApiKeys.Domain.ApiKeyEntry.TenantId",            // [PERMANENT] tenant scope
        "Granit.Authorization.Domain.PermissionGrant.TenantId",                 // [PERMANENT] tenant scope
        "Granit.Authorization.Domain.RoleMetadata.TenantId",                    // [PERMANENT] tenant scope
        "Granit.BlobStorage.Domain.BlobDescriptor.TenantId",                    // [PERMANENT] tenant scope
        "Granit.DataExchange.Export.Domain.ExportJob.TenantId",                 // [PERMANENT] tenant scope
        "Granit.DataExchange.Import.Domain.ImportJob.TenantId",                 // [PERMANENT] tenant scope
        "Granit.Identity.Domain.User.TenantId",                                  // [PERMANENT] tenant scope
        "Granit.Identity.Federated.Domain.FederatedIdentity.TenantId",          // [PERMANENT] tenant scope
        "Granit.Identity.Local.Domain.GranitUserGroup.TenantId",               // [PERMANENT] tenant scope
        "Granit.Localization.Domain.LocalizationOverride.TenantId",            // [PERMANENT] tenant scope
        "Granit.Notifications.Domain.NotificationPreference.TenantId",          // [PERMANENT] tenant scope
        "Granit.Notifications.Domain.UserNotification.TenantId",                // [PERMANENT] tenant scope
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication.TenantId", // [PERMANENT] tenant scope
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope.TenantId", // [PERMANENT] tenant scope
        "Granit.Scheduling.Domain.ScheduledAction.TenantId",                   // [PERMANENT] tenant scope
        "Granit.Timeline.Domain.TimelineEntry.TenantId",                       // [PERMANENT] tenant scope
        "Granit.Webhooks.Domain.WebhookDeliveryAttempt.TenantId",             // [PERMANENT] tenant scope
        "Granit.Webhooks.Domain.WebhookSubscription.TenantId",                 // [PERMANENT] tenant scope
        "Granit.Workflow.Domain.WorkflowTransitionRecord.TenantId",            // [PERMANENT] tenant scope
    };
}
