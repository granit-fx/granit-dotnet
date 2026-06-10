using Granit.AI.Ollama.Internal;
using Granit.AI.Tenancy;
using Granit.Events;
using Granit.Settings.Events;

namespace Granit.AI.Ollama.Handlers;

/// <summary>
/// Flushes the Ollama SDK client cache when an Ollama endpoint setting changes, so a redirected
/// or removed endpoint stops serving traffic immediately instead of lingering until the cache's
/// sliding-expiration TTL elapses.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="SettingChangedEvent"/> and reacts only to settings under the
/// <c>Granit.AI.Ollama.</c> prefix (see <see cref="AISettingNames.Ollama"/>). The whole provider
/// cache is cleared via <see cref="OllamaClientCache.InvalidateAll"/> rather than evicting a
/// single key: the cache is keyed by endpoint+model+scope, so a single endpoint change can affect
/// many cached clients. Endpoint changes are rare, so the coarse flush is acceptable.
/// </remarks>
internal sealed class OllamaCredentialCacheInvalidationHandler(OllamaClientCache cache)
    : ILocalEventHandler<SettingChangedEvent>
{
    private const string SettingPrefix = AISettingNames.PrefixValue + "Ollama.";

    /// <inheritdoc/>
    public Task HandleAsync(SettingChangedEvent localEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localEvent);

        if (localEvent.SettingName.StartsWith(SettingPrefix, StringComparison.Ordinal))
        {
            cache.InvalidateAll();
        }

        return Task.CompletedTask;
    }
}
