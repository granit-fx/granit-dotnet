using Granit.AI.Anthropic.Internal;
using Granit.AI.Tenancy;
using Granit.Events;
using Granit.Settings.Events;

namespace Granit.AI.Anthropic.Handlers;

/// <summary>
/// Flushes the Anthropic SDK client cache when an Anthropic credential setting changes, so a
/// rotated or revoked key stops serving traffic immediately instead of lingering until the
/// cache's sliding-expiration TTL elapses.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="SettingChangedEvent"/> and reacts only to settings under the
/// <c>Granit.AI.Anthropic.</c> prefix (see <see cref="AISettingNames.Anthropic"/>). The new
/// credential is never read from the event: the affected cache key is SHA-256(previous key),
/// which cannot be recomputed without the plaintext the event deliberately omits, so the whole
/// provider cache is cleared via <see cref="AnthropicClientCache.InvalidateAll"/>. Credential
/// changes are rare, so the coarse flush is acceptable.
/// </remarks>
internal sealed class AnthropicCredentialCacheInvalidationHandler(AnthropicClientCache cache)
    : ILocalEventHandler<SettingChangedEvent>
{
    private const string SettingPrefix = AISettingNames.PrefixValue + "Anthropic.";

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
