using Granit.Auditing.Domain;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Granit.BackgroundJobs.Domain;
using Granit.CustomerBalance.Domain;
using Granit.DataExchange.Definitions.Auditing;
using Granit.DataExchange.Definitions.Authorization;
using Granit.DataExchange.Definitions.BackgroundJobs;
using Granit.DataExchange.Definitions.CustomerBalance;
using Granit.DataExchange.Definitions.Identity;
using Granit.DataExchange.Definitions.Localization;
using Granit.DataExchange.Definitions.Metering;
using Granit.DataExchange.Definitions.MultiTenancy;
using Granit.DataExchange.Definitions.Notifications;
using Granit.DataExchange.Definitions.OpenIddict;
using Granit.DataExchange.Definitions.Payments;
using Granit.DataExchange.Definitions.QueryEngine;
using Granit.DataExchange.Definitions.ReferenceData;
using Granit.DataExchange.Definitions.Settings;
using Granit.DataExchange.Definitions.Tax;
using Granit.DataExchange.Definitions.Timeline;
using Granit.DataExchange.Definitions.Workflow;
using Granit.DataExchange.Export.Domain;
using Granit.DataExchange.Extensions;
using Granit.DataExchange.Import.Domain;
using Granit.Identity.Federated.Domain;
using Granit.Identity.Local.Domain;
using Granit.Localization.EntityFrameworkCore.Entities;
using Granit.Metering.Domain;
using Granit.MultiTenancy.EntityFrameworkCore.Entities;
using Granit.Notifications.Domain;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.Payments.Domain;
using Granit.Payments.SepaDirectDebit.Domain;
using Granit.QueryEngine.SavedViews.Domain;
using Granit.ReferenceData.Domain;
using Granit.Settings.EntityFrameworkCore.Entities;
using Granit.Tax.Domain;
using Granit.Timeline.Domain;
using Granit.Workflow.Domain;
using Microsoft.Extensions.DependencyInjection;
using DataExchangeDefinitions = Granit.DataExchange.Definitions.DataExchange;

namespace Granit.DataExchange.Definitions.Extensions;

/// <summary>
/// Registers all pre-built <c>ExportDefinition&lt;T&gt;</c> implementations
/// for Granit framework entities.
/// </summary>
[System.Obsolete("Each module now owns its own ExportDefinition registrations (see ADR-020). This package will be removed once the migration is complete.")]
public static class DataExchangeDefinitionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers export definitions for Granit framework entities.
    /// Internal entities (AIWorkspaceEntity, AIUsageRecordEntity, TenantFeatureOverride)
    /// are excluded — they use the reflection-based fallback within their own assemblies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitExportDefinitions(this IServiceCollection services)
    {
        // Priority 1: Core admin entities
        services.AddExportDefinition<Tenant, TenantExportDefinition>();
        services.AddExportDefinition<GranitRole, GranitRoleExportDefinition>();
        services.AddExportDefinition<GranitUserGroup, GranitUserGroupExportDefinition>();
        services.AddExportDefinition<UserCacheEntry, UserCacheEntryExportDefinition>();
        services.AddExportDefinition<PermissionGrant, PermissionGrantExportDefinition>();
        services.AddExportDefinition<DynamicReferenceDataEntity, DynamicReferenceDataEntityExportDefinition>();
        services.AddExportDefinition<GranitOpenIddictApplication, OpenIddictApplicationExportDefinition>();
        services.AddExportDefinition<GranitOpenIddictScope, OpenIddictScopeExportDefinition>();

        // Priority 2: Business & SaaS
        services.AddExportDefinition<PaymentTransaction, PaymentTransactionExportDefinition>();
        services.AddExportDefinition<PaymentMethod, PaymentMethodExportDefinition>();
        services.AddExportDefinition<Mandate, MandateExportDefinition>();
        services.AddExportDefinition<BalanceAccount, BalanceAccountExportDefinition>();
        services.AddExportDefinition<BalanceTransaction, BalanceTransactionExportDefinition>();
        services.AddExportDefinition<MeterDefinition, MeterDefinitionExportDefinition>();
        services.AddExportDefinition<UsageAggregate, UsageAggregateExportDefinition>();
        services.AddExportDefinition<TaxRateOverride, TaxRateOverrideExportDefinition>();

        // Priority 3: Operations & infrastructure
        services.AddExportDefinition<AuditEntry, AuditEntryExportDefinition>();
        services.AddExportDefinition<AuditEntityChange, AuditEntityChangeExportDefinition>();
        services.AddExportDefinition<BackgroundJobDefinition, BackgroundJobDefinitionExportDefinition>();
        services.AddExportDefinition<UserNotification, UserNotificationExportDefinition>();
        services.AddExportDefinition<NotificationPreference, NotificationPreferenceExportDefinition>();
        services.AddExportDefinition<TimelineEntry, TimelineEntryExportDefinition>();
        services.AddExportDefinition<SettingRecord, SettingRecordExportDefinition>();
        services.AddExportDefinition<LocalizationOverride, LocalizationOverrideExportDefinition>();
        services.AddExportDefinition<ImportJob, DataExchangeDefinitions.ImportJobExportDefinition>();
        services.AddExportDefinition<ExportJob, DataExchangeDefinitions.ExportJobExportDefinition>();

        // Priority 4: Domain-specific
        services.AddExportDefinition<WorkflowTransitionRecord, WorkflowTransitionRecordExportDefinition>();
        services.AddExportDefinition<SavedView, SavedViewExportDefinition>();
        services.AddExportDefinition<Dispute, DisputeExportDefinition>();
        services.AddExportDefinition<Refund, RefundExportDefinition>();

        return services;
    }
}
