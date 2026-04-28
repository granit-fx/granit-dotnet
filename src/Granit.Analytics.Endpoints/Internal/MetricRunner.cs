using System.Globalization;
using Granit.Analytics.EntityFrameworkCore;
using Granit.Analytics.Metrics;
using Granit.QueryEngine;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Typed implementation of <see cref="IMetricRunner"/> — closes over <typeparamref name="TEntity"/>
/// and <typeparamref name="TValue"/> so the HTTP endpoint can dispatch by name
/// without reflection at request time.
/// </summary>
internal sealed class MetricRunner<TEntity, TValue>(
    MetricDefinition<TEntity, TValue> definition,
    IQueryableSource<TEntity> source,
    IMetricExecutor<TEntity, TValue> executor) : IMetricRunner
    where TEntity : class
    where TValue : struct
{
    private readonly MetricDefinition<TEntity, TValue> _definition = definition;
    private readonly IQueryableSource<TEntity> _source = source;
    private readonly IMetricExecutor<TEntity, TValue> _executor = executor;

    public string Name => _definition.Name;

    public RefreshHint RefreshHint => _definition.RefreshHint;

    public bool SupportsPeriod => _definition.PeriodSelector is not null;

    public MetricValueKind ValueKind => _definition.ValueKind;

    public string? CurrencyCode => _definition.CurrencyCode;

    public bool IsHigherBetter => _definition.IsHigherBetter;

    public async Task<decimal?> ExecuteAsync(ResolvedPeriod? period, CancellationToken cancellationToken)
    {
        IQueryable<TEntity> queryable = _source.GetQueryable();

        if (period is { } resolved && _definition.PeriodSelector is not null)
        {
            queryable = PeriodFilterBuilder.ApplyPeriod(queryable, _definition.PeriodSelector, resolved);
        }

        TValue? raw = await _executor.ExecuteAsync(_definition, queryable, new QueryRequest(), cancellationToken)
            .ConfigureAwait(false);

        return raw.HasValue
            ? Convert.ToDecimal(raw.Value, CultureInfo.InvariantCulture)
            : null;
    }
}
