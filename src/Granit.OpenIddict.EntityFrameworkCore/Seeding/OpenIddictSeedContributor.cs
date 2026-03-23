using System.Text.Json;
using Granit.OpenIddict.Options;
using Granit.Persistence.DataSeeding;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.EntityFrameworkCore.Seeding;

/// <summary>
/// Seeds OIDC applications and scopes at startup using OpenIddict managers.
/// Idempotent: upserts based on <c>ClientId</c> / <c>Name</c>.
/// </summary>
internal sealed partial class OpenIddictSeedContributor(
    IOpenIddictApplicationManager applicationManager,
    IOpenIddictScopeManager scopeManager,
    IOptions<GranitOpenIddictSeedingOptions> options,
    ILogger<OpenIddictSeedContributor> logger) : IDataSeedContributor
{
    /// <summary>
    /// Standard OIDC scopes seeded automatically on every startup.
    /// These are the identity resources defined by the OpenID Connect specification.
    /// </summary>
    private static readonly OidcScopeSeedDescriptor[] StandardScopes =
    [
        new("openid", "OpenID (subject identifier)", []),
        new("profile", "User profile (name, family_name, given_name, preferred_username)", []),
        new("email", "Email address (email, email_verified)", []),
        new("phone", "Phone number (phone_number, phone_number_verified)", []),
        new("address", "Postal address", []),
        new("roles", "User roles", []),
        new("offline_access", "Refresh token (offline access)", []),
    ];

    /// <inheritdoc/>
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        GranitOpenIddictSeedingOptions seedOptions = options.Value;

        // 1. Always seed standard OIDC scopes
        foreach (OidcScopeSeedDescriptor scope in StandardScopes)
        {
            await SeedScopeAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        // 2. Seed user-configured applications
        foreach (OidcApplicationSeedDescriptor app in seedOptions.Applications)
        {
            await SeedApplicationAsync(app, cancellationToken).ConfigureAwait(false);
        }

        // 3. Seed user-configured scopes
        foreach (OidcScopeSeedDescriptor scope in seedOptions.Scopes)
        {
            await SeedScopeAsync(scope, cancellationToken).ConfigureAwait(false);
        }

        Log.SeedingCompleted(logger,
            StandardScopes.Length + seedOptions.Scopes.Length,
            seedOptions.Applications.Length);
    }

    private async Task SeedApplicationAsync(
        OidcApplicationSeedDescriptor descriptor, CancellationToken cancellationToken)
    {
        object? existing = await applicationManager.FindByClientIdAsync(descriptor.ClientId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var appDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = descriptor.ClientId,
                ClientSecret = descriptor.ClientSecret,
                DisplayName = descriptor.DisplayName,
            };

            foreach (string permission in descriptor.Permissions)
            {
                appDescriptor.Permissions.Add(permission);
            }

            foreach (string uri in descriptor.RedirectUris)
            {
                appDescriptor.RedirectUris.Add(new Uri(uri));
            }

            foreach (string uri in descriptor.PostLogoutRedirectUris)
            {
                appDescriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            // Register client's public key for private_key_jwt authentication (RFC 7523)
            if (!string.IsNullOrEmpty(descriptor.SigningKeyJwk))
            {
                appDescriptor.JsonWebKeySet = BuildJsonWebKeySet(descriptor.SigningKeyJwk);
            }

            await applicationManager.CreateAsync(appDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ApplicationCreated(logger, descriptor.ClientId);
        }
        else
        {
            var appDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(appDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            appDescriptor.DisplayName = descriptor.DisplayName;

            appDescriptor.Permissions.Clear();
            foreach (string permission in descriptor.Permissions)
            {
                appDescriptor.Permissions.Add(permission);
            }

            appDescriptor.RedirectUris.Clear();
            foreach (string uri in descriptor.RedirectUris)
            {
                appDescriptor.RedirectUris.Add(new Uri(uri));
            }

            appDescriptor.PostLogoutRedirectUris.Clear();
            foreach (string uri in descriptor.PostLogoutRedirectUris)
            {
                appDescriptor.PostLogoutRedirectUris.Add(new Uri(uri));
            }

            // Update client's public key for private_key_jwt authentication (RFC 7523)
            appDescriptor.JsonWebKeySet = !string.IsNullOrEmpty(descriptor.SigningKeyJwk)
                ? BuildJsonWebKeySet(descriptor.SigningKeyJwk)
                : null;

            await applicationManager.UpdateAsync(existing, appDescriptor, cancellationToken)
                .ConfigureAwait(false);
            Log.ApplicationUpdated(logger, descriptor.ClientId);
        }
    }

    private async Task SeedScopeAsync(
        OidcScopeSeedDescriptor descriptor, CancellationToken cancellationToken)
    {
        object? existing = await scopeManager.FindByNameAsync(descriptor.Name, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            var scopeDescriptor = new OpenIddictScopeDescriptor
            {
                Name = descriptor.Name,
                DisplayName = descriptor.DisplayName,
            };

            foreach (string resource in descriptor.Resources)
            {
                scopeDescriptor.Resources.Add(resource);
            }

            await scopeManager.CreateAsync(scopeDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ScopeCreated(logger, descriptor.Name);
        }
        else
        {
            var scopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(scopeDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            scopeDescriptor.DisplayName = descriptor.DisplayName;

            scopeDescriptor.Resources.Clear();
            foreach (string resource in descriptor.Resources)
            {
                scopeDescriptor.Resources.Add(resource);
            }

            await scopeManager.UpdateAsync(existing, scopeDescriptor, cancellationToken)
                .ConfigureAwait(false);
            Log.ScopeUpdated(logger, descriptor.Name);
        }
    }

    /// <summary>
    /// Builds a <see cref="JsonWebKeySet"/> from a JWK JSON string,
    /// stripping private key parameters to store only the public key.
    /// </summary>
    private static JsonWebKeySet BuildJsonWebKeySet(string jwkJson)
    {
        var jwk = new JsonWebKey(jwkJson);

        // Strip private key parameters — only store the public key
        jwk.D = null;
        jwk.P = null;
        jwk.Q = null;
        jwk.DP = null;
        jwk.DQ = null;
        jwk.QI = null;

        // Set key use to signing (required by OpenIddict for assertion validation)
        jwk.Use = JsonWebKeyUseNames.Sig;

        var jwks = new JsonWebKeySet();
        jwks.Keys.Add(jwk);
        return jwks;
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "OpenIddict seeding completed: {ScopeCount} scope(s), {ApplicationCount} application(s).")]
        public static partial void SeedingCompleted(ILogger logger, int scopeCount, int applicationCount);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC application '{ClientId}'.")]
        public static partial void ApplicationCreated(ILogger logger, string clientId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC application '{ClientId}'.")]
        public static partial void ApplicationUpdated(ILogger logger, string clientId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC scope '{ScopeName}'.")]
        public static partial void ScopeCreated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC scope '{ScopeName}'.")]
        public static partial void ScopeUpdated(ILogger logger, string scopeName);
    }
}
