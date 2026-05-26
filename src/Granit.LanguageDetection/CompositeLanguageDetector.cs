namespace Granit.LanguageDetection;

/// <summary>
/// Priority-chain composite that delegates to each registered
/// <see cref="ILanguageDetector"/> in descending <see cref="ILanguageDetector.Priority"/>
/// order until one returns a non-<c>null</c> result.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <see cref="ILanguageDetector"/> by <c>AddGranitLanguageDetection</c>.
/// Concrete detectors (Lingua/Franc-trigram, AI provider, metadata-hint) plug in by
/// registering additional <see cref="ILanguageDetector"/> services in DI — the
/// composite picks them up via constructor injection. Ties on identical priority are
/// resolved by DI registration order.
/// </para>
/// <para>
/// <see cref="Priority"/> on the composite itself is <see cref="int.MaxValue"/> so a
/// composite can be safely nested inside an outer chain (rare, but supported).
/// </para>
/// </remarks>
public sealed class CompositeLanguageDetector : ILanguageDetector
{
    private readonly IReadOnlyList<ILanguageDetector> _ordered;

    public CompositeLanguageDetector(IEnumerable<ILanguageDetector> detectors)
    {
        ArgumentNullException.ThrowIfNull(detectors);
        _ordered = detectors
            .Where(d => d is not CompositeLanguageDetector)
            .OrderByDescending(d => d.Priority)
            .ToArray();
    }

    /// <inheritdoc/>
    public int Priority => int.MaxValue;

    /// <inheritdoc/>
    public async Task<string?> DetectAsync(string content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        foreach (ILanguageDetector detector in _ordered)
        {
            string? result = await detector.DetectAsync(content, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                return result;
            }
        }

        return null;
    }
}
