using System.Diagnostics;
using Granit.Analytics.Diagnostics;
using Granit.Analytics.Endpoints.Dtos;
using Granit.Analytics.Endpoints.Options;
using Granit.Analytics.Internal;
using Granit.Analytics.Metrics;
using Granit.Analytics.Rendering;
using Granit.Exceptions;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Orchestrates a single metric request: resolves the runner by name, applies
/// FusionCache (key includes tenant id — security boundary), evaluates period and
/// optional comparison window, and composes the response envelope.
/// </summary>
internal sealed class MetricEndpointService(
    IEnumerable<IMetricRunner> runners,
    PeriodResolver periodResolver,
    IFusionCache cache,
    IClock clock,
    IOptions<AnalyticsEndpointsOptions> options,
    ICurrentTenant? currentTenant = null)
{
    private readonly Dictionary<string, IMetricRunner> _runnerByName =
        runners.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);
    private readonly PeriodResolver _periodResolver = periodResolver;
    private readonly IFusionCache _cache = cache;
    private readonly IClock _clock = clock;
    private readonly AnalyticsEndpointsOptions _options = options.Value;
    private readonly ICurrentTenant? _currentTenant = currentTenant;

    public bool TryGetRunner(string metricName, out IMetricRunner runner)
    {
        return _runnerByName.TryGetValue(metricName, out runner!);
    }

    public async Task<MetricResponse> EvaluateAsync(
        IMetricRunner runner,
        MetricRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(request);

        ResolvedPeriod? mainPeriod = runner.SupportsPeriod
            ? _periodResolver.Resolve(request.Period)
            : null;

        ResolvedPeriod? comparePeriod = null;
        if (request.CompareTo is not null)
        {
            if (!runner.SupportsPeriod)
            {
                throw new BusinessRuleViolationException(
                    "Granit.Analytics:ComparisonWithoutPeriodSelector",
                    $"Metric '{runner.Name}' does not declare a PeriodSelector — comparison windows are not supported.");
            }

            comparePeriod = _periodResolver.ResolveComparison(request.CompareTo, mainPeriod!.Value);
        }

        string tenantId = _currentTenant is { IsAvailable: true, Id: { } id } ? id.ToString() : "global";

        decimal? currentValue = await GetWithCacheAsync(runner, tenantId, mainPeriod, isComparison: false, cancellationToken)
            .ConfigureAwait(false);

        MetricPreviousPayload? previous = null;
        if (comparePeriod.HasValue)
        {
            decimal? previousValue = await GetWithCacheAsync(runner, tenantId, comparePeriod, isComparison: true, cancellationToken)
                .ConfigureAwait(false);
            previous = DeltaCalculator.Build(currentValue, previousValue, runner.IsHigherBetter);
        }

        bool noData = !currentValue.HasValue;

        MetricSnapshotPayload snapshot = new(
            currentValue,
            runner.ValueKind,
            runner.CurrencyCode,
            runner.IsHigherBetter,
            noData,
            previous);

        return new MetricResponse(
            runner.Name,
            snapshot,
            Sequence: 1,
            EmittedAt: _clock.Now,
            RefreshHint: runner.RefreshHint);
    }

    private async Task<decimal?> GetWithCacheAsync(
        IMetricRunner runner,
        string tenantId,
        ResolvedPeriod? period,
        bool isComparison,
        CancellationToken cancellationToken)
    {
        TimeSpan ttl = runner.RefreshHint switch
        {
            RefreshHint.Static => _options.StaticTtl,
            RefreshHint.Dynamic => _options.DynamicTtl,
            _ => TimeSpan.Zero, // Realtime — bypass cache
        };

        string key = MetricCacheKey.Compose(
            runner.Name,
            tenantId,
            isComparison ? null : period,
            isComparison ? period : null);

        long startTimestamp = Stopwatch.GetTimestamp();
        if (ttl <= TimeSpan.Zero)
        {
            // Inline /metrics/{name} path — no dashboard filter context.
            return await runner.ExecuteAsync(period, dashboardFilters: null, cancellationToken).ConfigureAwait(false);
        }

        decimal? value = await _cache.GetOrSetAsync<decimal?>(
            key,
            async (_, ct) => await runner.ExecuteAsync(period, dashboardFilters: null, ct).ConfigureAwait(false),
            new FusionCacheEntryOptions { Duration = ttl },
            token: cancellationToken).ConfigureAwait(false);

        _ = Stopwatch.GetElapsedTime(startTimestamp); // observed via metrics in a future story

        return value;
    }
}
