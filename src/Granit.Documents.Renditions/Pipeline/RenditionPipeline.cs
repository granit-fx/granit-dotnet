using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Exceptions;
using Granit.Documents.Renditions.Options;
using Granit.Documents.Renditions.Providers;
using Microsoft.Extensions.Options;

namespace Granit.Documents.Renditions.Pipeline;

/// <summary>
/// Default BFS-based pipeline solver. Builds a graph of <c>(sourceContentType,
/// outputContentType)</c> edges from the registered <see cref="IRenditionProvider"/>
/// instances and finds the shortest chain to the requested target.
/// </summary>
/// <remarks>
/// <para>
/// Provider matching uses MIME pattern semantics — a provider whose
/// <see cref="IRenditionProvider.OutputContentType"/> is <c>"image/*"</c> matches any
/// <c>image/X</c> target, and a provider's <see cref="IRenditionProvider.CanHandle"/>
/// callback decides input acceptance. The solver caps chains at
/// <see cref="GranitRenditionsOptions.MaxChainLength"/> hops (default 3) so misconfigured
/// providers can't induce infinite searches.
/// </para>
/// <para>
/// When multiple providers can bridge the same content types, the solver picks the
/// shortest chain; ties break on registration order.
/// </para>
/// </remarks>
internal sealed class RenditionPipeline(
    IEnumerable<IRenditionProvider> providers,
    IOptions<GranitRenditionsOptions> options) : IRenditionPipeline
{
    private readonly IRenditionProvider[] _providers = [.. providers];

    /// <inheritdoc />
    public async Task<RenditionResult> ExecuteAsync(
        Stream source,
        string sourceContentType,
        RenditionTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceContentType);
        ArgumentNullException.ThrowIfNull(target);

        IRenditionProvider[] chain = SolveChain(sourceContentType, target.TargetContentType)
            ?? throw new RenditionPipelineException(
                $"No rendition provider chain bridges '{sourceContentType}' to '{target.TargetContentType}' " +
                $"within {options.Value.MaxChainLength} hops. Registered providers: " +
                $"[{string.Join(", ", _providers.Select(p => p.Name))}].");

        Stream current = source;
        string currentType = sourceContentType;
        RenditionResult? result = null;

        for (int i = 0; i < chain.Length; i++)
        {
            IRenditionProvider provider = chain[i];
            try
            {
                result = await provider
                    .GenerateAsync(current, currentType, target, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not RenditionPipelineException)
            {
                throw new RenditionPipelineException(
                    $"Rendition pipeline failed at provider '{provider.Name}' " +
                    $"while bridging '{sourceContentType}' to '{target.TargetContentType}': {ex.Message}",
                    ex);
            }

            // Hand the intermediate output to the next hop. The first iteration leaves
            // the caller's source untouched; later iterations free the previous Memory
            // stream we created here.
            if (i < chain.Length - 1)
            {
                if (i > 0 && current is MemoryStream previous)
                {
                    await previous.DisposeAsync().ConfigureAwait(false);
                }
                current = new MemoryStream(result.Content, writable: false);
                currentType = result.ContentType;
            }
        }

        return result!;
    }

    /// <inheritdoc />
    public bool CanBridge(string sourceContentType, string targetContentType) =>
        SolveChain(sourceContentType, targetContentType) is not null;

    /// <summary>BFS over the provider graph. Returns the shortest chain or <c>null</c> when no path exists.</summary>
    private IRenditionProvider[]? SolveChain(string sourceContentType, string targetContentType)
    {
        int maxHops = options.Value.MaxChainLength;
        if (maxHops <= 0)
        {
            return null;
        }

        // Direct hit: any single provider that handles the source and produces the target.
        foreach (IRenditionProvider provider in _providers)
        {
            if (provider.CanHandle(sourceContentType)
                && OutputMatches(provider.OutputContentType, targetContentType))
            {
                return [provider];
            }
        }

        if (maxHops < 2)
        {
            return null;
        }

        // BFS: queue entries carry (current MIME after last hop, chain so far).
        Queue<(string Type, IRenditionProvider[] Chain)> queue = new();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase) { sourceContentType };
        queue.Enqueue((sourceContentType, []));

        while (queue.Count > 0)
        {
            (string currentType, IRenditionProvider[] chain) = queue.Dequeue();
            if (chain.Length >= maxHops)
            {
                continue;
            }

            foreach (IRenditionProvider provider in _providers)
            {
                if (!provider.CanHandle(currentType))
                {
                    continue;
                }
                IRenditionProvider[] nextChain = [.. chain, provider];
                string nextType = provider.OutputContentType;

                if (OutputMatches(nextType, targetContentType))
                {
                    return nextChain;
                }
                if (visited.Add(nextType) && nextChain.Length < maxHops)
                {
                    queue.Enqueue((nextType, nextChain));
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether <paramref name="output"/> covers <paramref name="target"/>. Supports
    /// the trailing wildcard convention (<c>"image/*"</c> matches <c>"image/png"</c>) and
    /// exact equality.
    /// </summary>
    private static bool OutputMatches(string output, string target)
    {
        if (string.Equals(output, target, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (output.EndsWith("/*", StringComparison.Ordinal))
        {
            string prefix = output[..^1]; // keep the slash
            return target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
