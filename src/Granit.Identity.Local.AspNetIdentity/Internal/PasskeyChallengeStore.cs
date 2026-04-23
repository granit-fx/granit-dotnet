using Granit.Identity.Local.Options;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// Round-trips a serialised WebAuthn options payload across the two halves of a
/// passkey ceremony (begin → complete). Backed by <see cref="IFusionCache"/> so
/// the state survives pod rotation when a distributed L2 (Redis) is wired, and
/// stays in-process otherwise.
/// </summary>
/// <remarks>
/// <para>
/// FIDO2 requires the receiver to verify that the assertion's <c>clientDataJSON.challenge</c>
/// matches the challenge it issued in the <c>begin</c> step. We achieve that by storing
/// the full <c>CredentialCreateOptions</c> / <c>AssertionOptions</c> JSON keyed by a
/// stable per-ceremony token (the user id for registration, the challenge for the
/// anonymous assertion / Conditional UI flow). The completion handler retrieves and
/// rehydrates the options before passing them to <c>IFido2.MakeNewCredentialAsync</c>
/// or <c>IFido2.MakeAssertionAsync</c>, which check the challenge for us.
/// </para>
/// <para>
/// Entries expire after <see cref="GranitPasskeyOptions.ChallengeLifetime"/> (default 5 min)
/// so a captured challenge cannot be replayed long after the ceremony was abandoned.
/// </para>
/// </remarks>
internal sealed class PasskeyChallengeStore(
    IFusionCache cache,
    IOptions<GranitPasskeyOptions> options)
{
    private const string RegistrationKeyPrefix = "granit:identity:passkey:reg:";
    private const string AssertionKeyPrefix = "granit:identity:passkey:asn:";

    public ValueTask SaveRegistrationAsync(string userId, string optionsJson, CancellationToken cancellationToken) =>
        SaveAsync(RegistrationKeyPrefix + userId, optionsJson, cancellationToken);

    public Task<string?> ConsumeRegistrationAsync(string userId, CancellationToken cancellationToken) =>
        ConsumeAsync(RegistrationKeyPrefix + userId, cancellationToken);

    public ValueTask SaveAssertionAsync(string ceremonyId, string optionsJson, CancellationToken cancellationToken) =>
        SaveAsync(AssertionKeyPrefix + ceremonyId, optionsJson, cancellationToken);

    public Task<string?> ConsumeAssertionAsync(string ceremonyId, CancellationToken cancellationToken) =>
        ConsumeAsync(AssertionKeyPrefix + ceremonyId, cancellationToken);

    private ValueTask SaveAsync(string key, string payload, CancellationToken cancellationToken) =>
        cache.SetAsync(
            key,
            payload,
            new FusionCacheEntryOptions { Duration = options.Value.ChallengeLifetime },
            token: cancellationToken);

    private async Task<string?> ConsumeAsync(string key, CancellationToken cancellationToken)
    {
        string? payload = await cache.GetOrDefaultAsync<string?>(
            key, defaultValue: null, token: cancellationToken).ConfigureAwait(false);

        if (payload is not null)
        {
            // Burn the challenge after first use — per WebAuthn §13.1 challenges must
            // never be accepted twice.
            await cache.RemoveAsync(key, token: cancellationToken).ConfigureAwait(false);
        }
        return payload;
    }
}
