using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Resolves the signing material that should be used for an outbound webhook delivery.
/// Returns the unprotected plaintext of the subscription's
/// <see cref="WebhookSigningKeyStatus.Active"/> key — throws
/// <see cref="InvalidOperationException"/> when no active key is present (malformed
/// subscription).
/// </summary>
internal static class WebhookSecretResolver
{
    public static async Task<string> ResolvePlainSecretAsync(
        WebhookSubscription subscription,
        IWebhookSecretProtector secretProtector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        WebhookSigningKey? activeKey = null;
        foreach (WebhookSigningKey k in subscription.SigningKeys)
        {
            if (k.Status == WebhookSigningKeyStatus.Active)
            {
                activeKey = k;
                break;
            }
        }

        if (activeKey is null)
        {
            throw new InvalidOperationException(
                $"Subscription '{subscription.Id}' has no active signing key. "
              + "Rotate the key to introduce a new active key.");
        }

        return await secretProtector
            .UnprotectAsync(activeKey.ProtectedSecret, cancellationToken)
            .ConfigureAwait(false);
    }
}
