using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.Tenancy;
using Granit.Events;
using Granit.Settings.Events;

namespace Granit.AI.AzureOpenAI.Handlers;

/// <summary>
/// Flushes the Azure OpenAI SDK client cache when an Azure OpenAI credential setting changes, so
/// a rotated or revoked key (or a changed resource endpoint) stops serving traffic immediately
/// instead of lingering until the cache's sliding-expiration TTL elapses.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="SettingChangedEvent"/> and reacts only to settings under the
/// <c>Granit.AI.AzureOpenAI.</c> prefix (see <see cref="AISettingNames.AzureOpenAI"/>). The new
/// credential is never read from the event: the affected cache key is SHA-256(previous key) +
/// endpoint, which cannot be recomputed without the plaintext the event deliberately omits, so
/// the whole provider cache is cleared via <see cref="AzureOpenAIClientCache.InvalidateAll"/>.
/// Credential changes are rare, so the coarse flush is acceptable.
/// </remarks>
internal sealed class AzureOpenAICredentialCacheInvalidationHandler(AzureOpenAIClientCache cache)
    : ILocalEventHandler<SettingChangedEvent>
{
    private const string SettingPrefix = AISettingNames.PrefixValue + "AzureOpenAI.";

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
