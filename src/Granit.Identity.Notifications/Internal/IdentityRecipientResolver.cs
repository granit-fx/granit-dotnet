using Granit.Identity.Domain;
using Granit.Identity.Notifications.Options;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Notifications.Internal;

/// <summary>
/// Resolves <see cref="RecipientInfo"/> from the Identity module's
/// <see cref="IIdentityUserReader"/>. A single adapter covering local
/// (OpenIddict) and every federated provider, since
/// <see cref="IIdentityUserReader"/> is the unified read abstraction across
/// providers.
/// </summary>
/// <remarks>
/// Contact fields are read from the canonical <see cref="User"/> aggregate
/// (ADR-051) when the backend returns it; otherwise the resolver falls back to
/// the provider <see cref="IIdentityUser.Metadata"/> bag using the keys
/// configured on <see cref="IdentityRecipientResolverOptions"/>. In federated
/// mode <see cref="IIdentityUserReader"/> already reads through the user cache,
/// so resolution avoids a round-trip to the external identity provider.
/// </remarks>
internal sealed partial class IdentityRecipientResolver(
    IIdentityUserReader reader,
    IOptions<IdentityRecipientResolverOptions> options,
    ILogger<IdentityRecipientResolver> logger) : IRecipientResolver
{
    private readonly IdentityRecipientResolverOptions _options = options.Value;

    public async Task<RecipientInfo?> ResolveAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        IIdentityUser? user = await reader.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        if (user is null)
        {
            LogUserNotResolved(userId);
            return null;
        }

        return new RecipientInfo
        {
            UserId = userId,
            Email = user.Email,
            PhoneNumber = ResolvePhoneNumber(user),
            PreferredCulture = ResolvePreferredCulture(user),
            PreferredTimeZone = ResolvePreferredTimeZone(user),
            DisplayName = ResolveDisplayName(user),
        };
    }

    private string? ResolvePhoneNumber(IIdentityUser user)
    {
        if (user is User { PhoneNumber: { Length: > 0 } phoneNumber })
        {
            return phoneNumber;
        }

        return ProbeMetadata(user, _options.PhoneNumberMetadataKeys);
    }

    private string? ResolvePreferredCulture(IIdentityUser user)
    {
        if (user is User { PreferredLocale: { Length: > 0 } preferredLocale })
        {
            return preferredLocale;
        }

        return ProbeMetadata(user, _options.PreferredCultureMetadataKeys) ?? _options.DefaultCulture;
    }

    private string? ResolvePreferredTimeZone(IIdentityUser user)
    {
        if (user is User { Timezone: { Length: > 0 } tz })
        {
            return tz;
        }

        return ProbeMetadata(user, _options.TimeZoneMetadataKeys);
    }

    private static string? ResolveDisplayName(IIdentityUser user)
    {
        if (user is User { DisplayName: { Length: > 0 } displayName })
        {
            return displayName;
        }

        string composed = $"{user.FirstName} {user.LastName}".Trim();
        return composed.Length > 0 ? composed : user.Username;
    }

    private static string? ProbeMetadata(IIdentityUser user, IList<string> keys)
    {
        foreach (string key in keys)
        {
            if (user.Metadata.TryGetValue(key, out string? value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Recipient resolution failed: no identity user found for user ID {UserId}.")]
    private partial void LogUserNotResolved(string userId);
}
