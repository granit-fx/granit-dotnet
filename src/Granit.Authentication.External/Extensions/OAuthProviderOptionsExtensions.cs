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

        provider.ApplyCallbackAndScopes(path => options.CallbackPath = path, options.Scope);
    }

    /// <summary>
    /// Applies the provider's optional callback-path override and scope overrides to a handler.
    /// Shared by the OAuth handlers and the generic OpenID Connect handler, whose option types
    /// (<see cref="OAuthOptions"/> and <c>OpenIdConnectOptions</c>) do not share a base exposing
    /// <c>CallbackPath</c>/<c>Scope</c> — hence the callback-path setter and scope collection are
    /// passed in rather than the options object.
    /// </summary>
    public static void ApplyCallbackAndScopes(
        this ExternalAuthProvider provider,
        Action<string> setCallbackPath,
        ICollection<string> scope)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(setCallbackPath);
        ArgumentNullException.ThrowIfNull(scope);

        if (!string.IsNullOrWhiteSpace(provider.CallbackPath))
        {
            setCallbackPath(provider.CallbackPath);
        }

        if (provider.Scopes.Length > 0)
        {
            scope.Clear();
            foreach (string s in provider.Scopes)
            {
                scope.Add(s);
            }
        }
    }
}
