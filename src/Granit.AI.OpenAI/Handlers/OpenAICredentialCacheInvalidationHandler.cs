using Granit.AI.OpenAI.Internal;
using Granit.AI.Tenancy;
using Granit.Events;
using Granit.Settings.Events;

namespace Granit.AI.OpenAI.Handlers;

/// <summary>
/// Flushes the OpenAI SDK client cache when an OpenAI credential setting changes, so a rotated
/// or revoked key (or a changed endpoint) stops serving traffic immediately instead of lingering
/// until the cache's sliding-expiration TTL elapses.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="SettingChangedEvent"/> and reacts only to settings under the
/// <c>Granit.AI.OpenAI.</c> prefix (see <see cref="AISettingNames.OpenAI"/>). The new credential
/// is never read from the event: the affected cache key is SHA-256(previous key) + endpoint,
/// which cannot be recomputed without the plaintext the event deliberately omits, so the whole
/// provider cache is cleared via <see cref="OpenAIClientCache.InvalidateAll"/>. The prefix is
/// checked ordinally, so <c>Granit.AI.AzureOpenAI.*</c> settings (a distinct provider) do not
/// match. Credential changes are rare, so the coarse flush is acceptable.
/// </remarks>
internal sealed class OpenAICredentialCacheInvalidationHandler(OpenAIClientCache cache)
    : ILocalEventHandler<SettingChangedEvent>
{
    private const string SettingPrefix = AISettingNames.PrefixValue + "OpenAI.";

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
