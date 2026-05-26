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
    private readonly IReadOnlyList<ILanguageDetectorProvider> _ordered;

    public CompositeLanguageDetector(IEnumerable<ILanguageDetectorProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        _ordered = providers
            .OrderByDescending(d => d.Priority)
            .ToArray();
    }

    /// <inheritdoc/>
    public int Priority => int.MaxValue;

    /// <inheritdoc/>
    public async Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (ILanguageDetectorProvider provider in _ordered)
        {
            string? result = await provider.DetectAsync(content, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return null;
    }
}
