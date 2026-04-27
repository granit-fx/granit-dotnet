using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Resolves the signing material that should be used for an outbound webhook delivery.
/// </summary>
/// <remarks>
/// <para>
/// Selection rule (FU-1a — dual-key delivery):
/// </para>
/// <list type="number">
///   <item>If the subscription has a <see cref="WebhookSigningKeyStatus.Active"/> key,
///     unprotect and return its <see cref="WebhookSigningKey.ProtectedSecret"/>.</item>
///   <item>Otherwise (no active key — typically a legacy subscription created before the
///     dual-key model and never rotated), fall back to the legacy
///     <see cref="WebhookSubscription.SigningSecret"/> if it is non-null.</item>
/// </list>
/// <para>
/// Throws <see cref="InvalidOperationException"/> if neither source is available — that
/// would indicate a malformed subscription (no active key AND no legacy secret).
/// </para>
/// </remarks>
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

        if (activeKey is not null)
        {
            return await secretProtector
                .UnprotectAsync(activeKey.ProtectedSecret, cancellationToken)
                .ConfigureAwait(false);
        }

#pragma warning disable CS0618 // Legacy fallback for subscriptions never rotated since the upgrade.
        string? legacy = subscription.SigningSecret;
#pragma warning restore CS0618

        if (!string.IsNullOrEmpty(legacy))
        {
            return await secretProtector
                .UnprotectAsync(legacy, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new InvalidOperationException(
            $"Subscription '{subscription.Id}' has no active signing key and no legacy SigningSecret. "
          + "Rotate the key to seed the dual-key model.");
    }
}
