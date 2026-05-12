using Granit.Dashboards;
using Granit.QueryEngine.Filtering;

namespace Granit.Analytics.Metrics;

/// <summary>
/// Marker interface for metric definition discovery via DI.
/// </summary>
/// <remarks>
/// <see cref="MetricDefinition{TEntity, TValue}"/> implements this so that hosts can resolve
/// every registered metric without knowing the closed generic types up front.
/// </remarks>
public interface IMetricDefinitionDescriptor
{
    /// <summary>Unique name identifying this metric (e.g. <c>"Granit.Invoicing.UnpaidInvoiceCount"</c>).</summary>
    string Name { get; }

    /// <summary>The entity type this metric aggregates.</summary>
    Type EntityType { get; }

    /// <summary>The CLR type of the metric's resulting value (e.g. <c>typeof(int)</c>, <c>typeof(decimal)</c>).</summary>
    Type ValueType { get; }

    /// <summary>The semantic kind of the value (count, currency, percentage, ...).</summary>
    MetricValueKind ValueKind { get; }

    /// <summary>The aggregation function applied (count, sum, avg, min, max).</summary>
    AggregateFunction Aggregation { get; }

    /// <summary>Whether higher values are considered favorable for the displayed delta.</summary>
    bool IsHigherBetter { get; }

    /// <summary>How fresh the metric's data is — drives caching TTL and pull / push transport.</summary>
    RefreshHint RefreshHint { get; }
}
