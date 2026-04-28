using System.Reflection;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;
using Shouldly;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Enforces the Query ↔ Metric pairing rule from EPIC #1366 (story #1396) — every
/// admin-visible entity that has a <see cref="QueryDefinition{TEntity}"/> SHOULD ship
/// at least one <see cref="MetricDefinition{TEntity, TValue}"/> so the admin grid is
/// always paired with at least one KPI tile.
/// </summary>
/// <remarks>
/// <para>
/// The framework cannot mechanically distinguish "admin-visible" from "internal
/// infrastructure" entities — the heuristic in story #1396 ("has a CRUD endpoint
/// group") would require parsing every <c>*Endpoints</c> module's route registration,
/// which is fragile. Instead, we invert the question: **every entity with a
/// QueryDefinition is by default admin-visible** and must therefore be paired with a
/// metric. Entities that are genuinely infrastructure (audit-log details, internal
/// config rows, ephemeral cache tables) opt out via the <see cref="PairingExemptions"/>
/// allow-list with a written justification.
/// </para>
/// <para>
/// The test is enforcement-mode: failure is an error, not a warning. New
/// QueryDefinitions that don't ship a matching MetricDefinition either:
/// </para>
/// <list type="number">
///   <item>add the metric in the module's <c>Metrics/</c> folder, registered via
///         <c>services.AddMetricDefinition&lt;TEntity, TValue, TDefinition&gt;()</c>; or</item>
///   <item>add the entity to <see cref="PairingExemptions"/> with a one-line
///         justification explaining why this entity has no business KPI worth
///         exposing in the admin UI.</item>
/// </list>
/// <para>
/// The companion <c>QueryExportPairingTests</c> uses the same shape for the
/// already-codified Query ↔ Export pairing.
/// </para>
/// </remarks>
public sealed class QueryMetricPairingTests
{
    /// <summary>
    /// Entities exempted from the Query ↔ Metric pairing rule. Each entry MUST carry a
    /// one-line justification (inline comment).
    /// </summary>
    /// <remarks>
    /// This list is pre-populated with the current baseline at the time of D1's
    /// introduction (EPIC #1366). Entries marked <c>[BACKLOG]</c> are admin-visible
    /// entities that should eventually ship a metric — they are exempted today so the
    /// rule can be enforced going forward without forcing a framework-wide retrofit.
    /// Removing an entry from <c>[BACKLOG]</c> requires shipping at least one
    /// MetricDefinition for that entity.
    ///
    /// Entries marked <c>[INFRA]</c> are genuine infrastructure / audit / internal cache
    /// entities that have no useful KPI on the admin UI — these are permanent
    /// exemptions.
    /// </remarks>
    private static readonly HashSet<string> PairingExemptions = new(StringComparer.Ordinal)
    {
        // ── [INFRA] permanent exemptions ────────────────────────────────────
        // System / audit / configuration entities with no useful business KPI on the
        // admin UI. These are not "admin-visible" in the grid-with-KPI sense — they
        // either back tooling (saved views, exports), record auditing trails, or
        // hold security / config state.

        "Granit.AI.AIUsageRecord",                                                        // [INFRA] AI cost / audit log
        "Granit.Auditing.Domain.AuditEntityChange",                                       // [INFRA] audit log
        "Granit.Auditing.Domain.AuditEntry",                                              // [INFRA] audit log
        "Granit.Authorization.Domain.PermissionGrant",                                    // [INFRA] RBAC config
        "Granit.Authorization.Domain.RoleMetadata",                                       // [INFRA] RBAC config
        "Granit.BackgroundJobs.Domain.BackgroundJobDefinition",                           // [INFRA] job config
        "Granit.DataExchange.Export.Domain.ExportJob",                                    // [INFRA] transient export job
        "Granit.DataExchange.Import.Domain.ImportJob",                                    // [INFRA] transient import job
        "Granit.Identity.Federated.Domain.UserCacheEntry",                                // [INFRA] internal user cache
        "Granit.Identity.Local.Domain.GranitRole",                                        // [INFRA] RBAC config
        "Granit.Identity.Local.Domain.GranitUserGroup",                                   // [INFRA] RBAC config
        "Granit.Localization.Domain.LocalizationOverride",                                // [INFRA] localization config
        "Granit.Metering.Domain.MeterDefinition",                                         // [INFRA] metering config
        "Granit.MultiTenancy.Domain.Tenant",                                              // [INFRA] platform-admin entity
        "Granit.Notifications.Domain.NotificationPreference",                             // [INFRA] user preference config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication",              // [INFRA] OAuth client config
        "Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope",                    // [INFRA] OAuth scope config
        "Granit.Parties.EntityFrameworkCore.Entities.PartyDuplicateCandidate",            // [INFRA] deduplication queue
        "Granit.QueryEngine.SavedViews.Domain.SavedView",                                 // [INFRA] grid tooling
        "Granit.ReferenceData.Domain.DynamicReferenceDataEntity",                         // [INFRA] reference-data config
        "Granit.Scheduling.Domain.ScheduledAction",                                       // [INFRA] scheduling state
        "Granit.Settings.Domain.SettingRecord",                                           // [INFRA] settings config
        "Granit.Tax.Domain.TaxRateOverride",                                              // [INFRA] tax config
        "Granit.Tax.TaxRateEntry",                                                        // [INFRA] tax config
        "Granit.Timeline.Domain.TimelineEntry",                                           // [INFRA] audit log
        "Granit.Workflow.Domain.WorkflowTransitionRecord",                                // [INFRA] workflow audit

        // ── [BACKLOG] admin-visible entities awaiting their first metric ────
        // These DO surface in admin grids and SHOULD ship at least one MetricDefinition.
        // They are exempted here as a known-debt baseline at the time D1 (#1396)
        // landed (post-A4 #1377). Remove an entry from this list when the matching
        // module ships its first MetricDefinition for that entity — the rule then
        // begins enforcing for that entity going forward.

        "Granit.BlobStorage.Domain.BlobDescriptor",                                       // [BACKLOG] storage-usage KPIs
        "Granit.Catalog.Domain.Product",                                                  // [BACKLOG] catalog count / activation
        "Granit.CustomerBalance.Domain.BalanceAccount",                                   // [BACKLOG] balance totals
        "Granit.CustomerBalance.Domain.BalanceTransaction",                               // [BACKLOG] credit / debit volume
        "Granit.Metering.Domain.UsageAggregate",                                          // [BACKLOG] usage trends
        "Granit.Notifications.Domain.UserNotification",                                   // [BACKLOG] delivery / read-rate KPIs
        "Granit.Parties.Domain.Party",                                                    // [BACKLOG] active-party count
        "Granit.Payments.Domain.Dispute",                                                 // [BACKLOG] open-dispute count / total
        "Granit.Payments.Domain.PaymentMethod",                                           // [BACKLOG] active-method count
        "Granit.Payments.Domain.PaymentTransaction",                                      // [BACKLOG] success-rate / volume
        "Granit.Payments.Domain.Refund",                                                  // [BACKLOG] refund-volume KPIs
        "Granit.Payments.SepaDirectDebit.Domain.Mandate",                                 // [BACKLOG] active-mandate count
        "Granit.Subscriptions.Domain.Plan",                                               // [BACKLOG] active-plan count
        "Granit.Subscriptions.Domain.PlanPrice",                                          // [BACKLOG] active-pricing count
        "Granit.Subscriptions.Domain.Subscription",                                       // [BACKLOG] MRR / ARR / churn rate
        "Granit.Webhooks.Domain.WebhookDeliveryAttempt",                                  // [BACKLOG] delivery failure rate
        "Granit.Webhooks.Domain.WebhookSubscription",                                     // [BACKLOG] active-subscription count
    };

    [Fact]
    public void Every_QueryDefinition_should_have_a_matching_MetricDefinition()
    {
        (HashSet<Type> queryEntities, HashSet<Type> metricEntities) = ScanEntities();

        IEnumerable<string> queryWithoutMetric = queryEntities
            .Where(t => !metricEntities.Contains(t) && !PairingExemptions.Contains(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        queryWithoutMetric.ShouldBeEmpty(
            "Pairing rule (EPIC #1366 / story #1396): every entity with a QueryDefinition " +
            "should also have at least one MetricDefinition. " +
            "Add a `*MetricDefinition` in the same module's `Metrics/` folder, " +
            "register it via `services.AddMetricDefinition<TEntity, TValue, TDefinition>()`, " +
            "or add the entity to PairingExemptions with a justification " +
            "(`[BACKLOG]` for admin-visible entities awaiting their first metric, " +
            "`[INFRA]` for permanent exemptions on infrastructure / audit / internal cache entities).");
    }

    [Fact]
    public void Every_MetricDefinition_should_have_a_matching_QueryDefinition()
    {
        // The inverse direction is strictly enforced — no exemption list. A metric
        // without a query underneath is always a bug: the user sees "12 unpaid invoices"
        // but cannot click through to a list of those 12 rows. Either the metric
        // is genuinely measuring something the admin UI never lists (rare; still
        // probably wrong) or the matching QueryDefinition is missing.
        (HashSet<Type> queryEntities, HashSet<Type> metricEntities) = ScanEntities();

        IEnumerable<string> metricWithoutQuery = metricEntities
            .Where(t => !queryEntities.Contains(t))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        metricWithoutQuery.ShouldBeEmpty(
            "Pairing rule (EPIC #1366 / story #1396): every entity with a MetricDefinition " +
            "must also have a QueryDefinition — a KPI tile is meaningless without an admin " +
            "list to drill down into. Add the matching `*QueryDefinition` in the same " +
            "module's `Queries/` folder and register it via " +
            "`services.AddQueryDefinition<TEntity, TDefinition>()`. " +
            "If the entity genuinely has no admin grid (rare), reconsider whether the " +
            "metric belongs in this module at all.");
    }

    private static (HashSet<Type> queryEntities, HashSet<Type> metricEntities) ScanEntities()
    {
        string outputDir = Path.GetDirectoryName(typeof(QueryMetricPairingTests).Assembly.Location)!;

        Assembly[] assemblies = Directory.GetFiles(outputDir, "Granit.*.dll")
            .Where(path =>
            {
                string name = Path.GetFileNameWithoutExtension(path);
                return !name.Contains("Tests", StringComparison.Ordinal)
                    && !name.EndsWith(".resources", StringComparison.Ordinal);
            })
            .Select(path =>
            {
                try { return Assembly.LoadFrom(path); }
                catch (Exception ex) when (ex is BadImageFormatException or FileLoadException) { return null; }
            })
            .Where(a => a is not null)
            .ToArray()!;

        HashSet<Type> queryEntities = [];
        HashSet<Type> metricEntities = [];

        foreach (Assembly assembly in assemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = [.. ex.Types.Where(t => t is not null)!]; }

            foreach (Type type in types)
            {
                if (type.IsAbstract || !type.IsClass)
                {
                    continue;
                }

                Type? queryEntity = ExtractGenericArgument(type, typeof(QueryDefinition<>));
                if (queryEntity is not null && queryEntity.FullName is not null)
                {
                    queryEntities.Add(queryEntity);
                }

                Type? metricEntity = ExtractMetricEntity(type);
                if (metricEntity is not null && metricEntity.FullName is not null)
                {
                    metricEntities.Add(metricEntity);
                }
            }
        }

        return (queryEntities, metricEntities);
    }

    private static Type? ExtractGenericArgument(Type candidate, Type openGenericBase)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == openGenericBase)
            {
                return cursor.GetGenericArguments()[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts <c>TEntity</c> from a concrete <c>MetricDefinition&lt;TEntity, TValue&gt;</c>.
    /// MetricDefinition has two generic parameters; we only care about the entity
    /// (presence of any metric for the entity, regardless of the value type, fulfils the
    /// pairing rule).
    /// </summary>
    private static Type? ExtractMetricEntity(Type candidate)
    {
        for (Type? cursor = candidate.BaseType; cursor is not null; cursor = cursor.BaseType)
        {
            if (cursor.IsGenericType && cursor.GetGenericTypeDefinition() == typeof(MetricDefinition<,>))
            {
                return cursor.GetGenericArguments()[0];
            }
        }

        return null;
    }
}
