using Granit.AI.Chat.Suggestions;
using Microsoft.Extensions.Logging;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAISuggestionResolver"/>. Fans out to every registered
/// <see cref="IAISuggestionProvider"/> and concatenates their suggestions, keeping the first of
/// each <see cref="AISuggestedAction.Type"/>. A provider that throws is skipped so one module
/// cannot break the turn.
/// </summary>
internal sealed partial class AISuggestionResolver(
    IEnumerable<IAISuggestionProvider> providers,
    ILogger<AISuggestionResolver> logger) : IAISuggestionResolver
{
    public async ValueTask<IReadOnlyList<AISuggestedAction>> ResolveAsync(
        AISuggestionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AISuggestedAction>? collected = null;
        HashSet<string>? seenTypes = null;

        foreach (IAISuggestionProvider provider in providers)
        {
            IReadOnlyList<AISuggestedAction> suggestions;
            try
            {
                suggestions = await provider.GetSuggestionsAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // A misbehaving suggestion provider must never fail the turn — its answer stands.
                // Log so a developer can see why suggestions are missing instead of debugging blind.
                LogProviderFailed(ex, provider.GetType().Name);
                continue;
            }

            foreach (AISuggestedAction suggestion in suggestions)
            {
                collected ??= [];
                seenTypes ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (seenTypes.Add(suggestion.Type))
                {
                    collected.Add(suggestion);
                }
            }
        }

        return collected ?? [];
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "AI suggestion provider '{Provider}' threw and was skipped; the turn continues without its suggestions.")]
    private partial void LogProviderFailed(Exception exception, string provider);
}
