using System.Diagnostics;
using Granit.LanguageDetection.Diagnostics;
using Granit.MultiTenancy;

namespace Granit.LanguageDetection;

/// <summary>
/// Priority-chain composite that delegates to each registered
/// <see cref="ILanguageDetectorProvider"/> in descending
/// <see cref="ILanguageDetector.Priority"/> order until one returns a non-<c>null</c>
/// result. The single <see cref="ILanguageDetector"/> exposed by the DI container.
/// </summary>
/// <remarks>
/// <para>
/// Registered by <c>AddGranitLanguageDetection</c> as the sole
/// <see cref="ILanguageDetector"/>. Provider packages (the bundled trigram detector,
/// AI-backed detectors, metadata-hint detectors) register concrete classes under
/// <see cref="ILanguageDetectorProvider"/> via <c>TryAddEnumerable</c>; the composite
/// picks them up at construction. Ties on identical priority are resolved by DI
/// registration order.
/// </para>
/// <para>
/// <see cref="Priority"/> on the composite itself is <see cref="int.MaxValue"/> so a
/// composite can be safely nested inside an outer chain (rare, but supported).
/// </para>
/// </remarks>
public sealed class CompositeLanguageDetector : ILanguageDetector
{
    private readonly ILanguageDetectorProvider[] _ordered;
    private readonly LanguageDetectionMetrics? _metrics;
    private readonly ICurrentTenant? _currentTenant;

    public CompositeLanguageDetector(IEnumerable<ILanguageDetectorProvider> providers)
        : this(providers, metrics: null, currentTenant: null)
    {
    }

    public CompositeLanguageDetector(
        IEnumerable<ILanguageDetectorProvider> providers,
        LanguageDetectionMetrics? metrics,
        ICurrentTenant? currentTenant)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _ordered = providers
            .OrderByDescending(d => d.Priority)
            .ToArray();
        _metrics = metrics;
        _currentTenant = currentTenant;
    }

    /// <inheritdoc/>
    public int Priority => int.MaxValue;

    /// <inheritdoc/>
    public async Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        string? tenantId = _currentTenant is { IsAvailable: true } tenant ? tenant.Id?.ToString() : null;

        using Activity? activity = LanguageDetectionActivitySource.Source.StartActivity(
            LanguageDetectionActivitySource.Detect,
            ActivityKind.Internal);
        activity?.SetTag("granit.tenant_id", tenantId ?? "global");
        activity?.SetTag("granit.language_detection.provider_count", _ordered.Length);

        long startTimestamp = Stopwatch.GetTimestamp();

        foreach (ILanguageDetectorProvider provider in _ordered)
        {
            string? result = await provider.DetectAsync(content, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                string detectorName = provider.GetType().Name;
                _metrics?.RecordHit(tenantId, detectorName, result);
                _metrics?.RecordLatency(tenantId, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
                activity?.SetTag("granit.language_detection.detector", detectorName);
                activity?.SetTag("granit.language_detection.result", "hit");
                return result;
            }
        }

        _metrics?.RecordMiss(tenantId);
        _metrics?.RecordLatency(tenantId, Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
        activity?.SetTag("granit.language_detection.result", "miss");
        return null;
    }
}
