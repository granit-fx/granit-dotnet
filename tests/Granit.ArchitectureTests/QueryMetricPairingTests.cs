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
    /// <c>[BACKLOG]</c> entities — admin-visible aggregates that <i>should</i>
    /// ship at least one <see cref="MetricDefinition{TEntity, TValue}"/> but
    /// haven't yet. These are exempted as a known-debt baseline at the time D1
    /// (#1396) landed (post-A4 #1377). Remove an entry when the owning module
    /// ships its first <c>MetricDefinition</c> for that entity — the rule then
    /// begins enforcing for that entity going forward.
    /// </summary>
    /// <remarks>
    /// <c>[INFRA]</c> exemptions live in the shared <see cref="PairingExemptions"/>
    /// set so Query↔Export and Query↔Metric stay in lockstep. Each entry below MUST
    /// carry a one-line justification (inline comment) describing the metric that
    /// would unblock removal.
    /// </remarks>
    private static readonly HashSet<string> MetricBacklog = new(StringComparer.Ordinal)
    {
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
        "Granit.Webhooks.Domain.WebhookDeliveryAttempt",                                  // [BACKLOG] delivery failure rate
        "Granit.Webhooks.Domain.WebhookSubscription",                                     // [BACKLOG] active-subscription count
    };

    private static bool IsExempt(string fullName) =>
        PairingExemptions.Infrastructure.Contains(fullName) || MetricBacklog.Contains(fullName);

    [Fact]
    public void Every_QueryDefinition_should_have_a_matching_MetricDefinition()
    {
        (HashSet<Type> queryEntities, HashSet<Type> metricEntities) = ScanEntities();

        IEnumerable<string> queryWithoutMetric = queryEntities
            .Where(t => !metricEntities.Contains(t) && !IsExempt(t.FullName!))
            .Select(t => t.FullName!)
            .OrderBy(s => s, StringComparer.Ordinal);

        queryWithoutMetric.ShouldBeEmpty(
            "Pairing rule (EPIC #1366 / story #1396): every entity with a QueryDefinition " +
            "should also have at least one MetricDefinition. " +
            "Add a `*MetricDefinition` in the same module's `Metrics/` folder, " +
            "register it via `services.AddMetricDefinition<TEntity, TValue, TDefinition>()`, " +
            "or add the entity to MetricBacklog (`[BACKLOG]` admin-visible entity awaiting its first metric) " +
            "or PairingExemptions.Infrastructure (`[INFRA]` permanent exemption on an audit / config / cache entity).");
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
