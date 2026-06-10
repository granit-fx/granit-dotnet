using Granit.Authentication.External.Options;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Identity;

namespace Granit.Authentication.External.Extensions;

/// <summary>
/// Applies common <see cref="ExternalAuthProvider"/> configuration to any OAuth-based handler
/// (Google, Microsoft, Facebook, GitHub, Apple — all derive from <see cref="OAuthOptions"/>).
/// </summary>
public static class OAuthProviderOptionsExtensions
{
    /// <summary>
    /// Copies client credentials, sign-in scheme, callback path and scopes from
    /// <paramref name="provider"/> onto the handler options.
    /// </summary>
    public static void ApplyExternalProvider(this OAuthOptions options, ExternalAuthProvider provider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(provider);

        options.ClientId = provider.ClientId;
        options.ClientSecret = provider.ClientSecret;

        // External providers sign into the Identity external cookie; the framework's callback
        // endpoint reads the resulting ticket from IdentityConstants.ExternalScheme.
        options.SignInScheme = IdentityConstants.ExternalScheme;

        if (!string.IsNullOrWhiteSpace(provider.CallbackPath))
        {
            options.CallbackPath = provider.CallbackPath;
        }

        if (provider.Scopes.Length > 0)
        {
            options.Scope.Clear();
            foreach (string scope in provider.Scopes)
            {
                options.Scope.Add(scope);
            }
        }
    }
}
