using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Options;
using Granit.Persistence.EntityFrameworkCore.DataSeeding;
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
    ILogger<OpenIddictSeedContributor> logger) : IHostDataSeedContributor
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
            };

            PopulateDescriptor(appDescriptor, descriptor);

            await applicationManager.CreateAsync(appDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ApplicationCreated(logger, descriptor.ClientId);
        }
        else
        {
            var appDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(appDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            // Skip update when values are already up to date — prevents ConcurrencyException
            // when migrator and API both execute seeders concurrently at startup.
            if (!ApplicationNeedsUpdate(appDescriptor, descriptor))
            {
                Log.ApplicationUnchanged(logger, descriptor.ClientId);
                return;
            }

            PopulateDescriptor(appDescriptor, descriptor);

            await applicationManager.UpdateAsync(existing, appDescriptor, cancellationToken)
                .ConfigureAwait(false);
            Log.ApplicationUpdated(logger, descriptor.ClientId);
        }
    }

    private static bool ApplicationNeedsUpdate(
        OpenIddictApplicationDescriptor current, OidcApplicationSeedDescriptor desired)
    {
        if (current.DisplayName != desired.DisplayName)
        {
            return true;
        }

        if (!current.Permissions.SetEquals(desired.Permissions))
        {
            return true;
        }

        if (!current.RedirectUris.SetEquals(desired.RedirectUris.Select(u => new Uri(u))))
        {
            return true;
        }

        if (!current.PostLogoutRedirectUris.SetEquals(desired.PostLogoutRedirectUris.Select(u => new Uri(u))))
        {
            return true;
        }

        JsonWebKeySet? desiredJwks = !string.IsNullOrEmpty(desired.SigningKeyJwk)
            ? BuildJsonWebKeySet(desired.SigningKeyJwk)
            : null;

        if (!JwksEqual(current.JsonWebKeySet, desiredJwks))
        {
            return true;
        }

        return current.GetClientSide() != desired.ClientSide;
    }

    /// <summary>
    /// Compares two <see cref="JsonWebKeySet"/> instances by public key material
    /// (Kty, Crv, N, E, X, Y, Use). Private key parameters are never stored.
    /// </summary>
    private static bool JwksEqual(JsonWebKeySet? a, JsonWebKeySet? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Keys.Count != b.Keys.Count)
        {
            return false;
        }

        IEnumerable<string> Fingerprints(JsonWebKeySet ks) =>
            ks.Keys
              .Select(k => $"{k.Kty}:{k.Crv}:{k.N}:{k.E}:{k.X}:{k.Y}:{k.Use}")
              .Order(StringComparer.Ordinal);

        return Fingerprints(a).SequenceEqual(Fingerprints(b));
    }

    /// <summary>
    /// Populates common fields of an <see cref="OpenIddictApplicationDescriptor"/>
    /// from the seed configuration. Collections are cleared before adding new values
    /// to support both create (no-op clear on empty collections) and update paths.
    /// </summary>
    private static void PopulateDescriptor(
        OpenIddictApplicationDescriptor appDescriptor,
        OidcApplicationSeedDescriptor source)
    {
        appDescriptor.DisplayName = source.DisplayName;

        appDescriptor.Permissions.Clear();
        foreach (string permission in source.Permissions)
        {
            appDescriptor.Permissions.Add(permission);
        }

        appDescriptor.RedirectUris.Clear();
        foreach (string uri in source.RedirectUris)
        {
            appDescriptor.RedirectUris.Add(new Uri(uri));
        }

        appDescriptor.PostLogoutRedirectUris.Clear();
        foreach (string uri in source.PostLogoutRedirectUris)
        {
            appDescriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        // Set client's public key for private_key_jwt authentication (RFC 7523)
        appDescriptor.JsonWebKeySet = !string.IsNullOrEmpty(source.SigningKeyJwk)
            ? BuildJsonWebKeySet(source.SigningKeyJwk)
            : null;

        // Consent type (default: implicit — auto-grant for first-party apps)
        appDescriptor.ConsentType = source.ConsentType ?? OpenIddictConstants.ConsentTypes.Implicit;

        // Host/tenant policy — persisted in Properties so the OIDC server can enforce
        // host-only / tenant-only client access without depending on Granit.Bff.
        appDescriptor.SetClientSide(source.ClientSide);
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

            // Skip update when values are already up to date — prevents ConcurrencyException
            // when migrator and API both execute seeders concurrently at startup.
            if (scopeDescriptor.DisplayName == descriptor.DisplayName
                && scopeDescriptor.Resources.SetEquals(descriptor.Resources))
            {
                Log.ScopeUnchanged(logger, descriptor.Name);
                return;
            }

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

        [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC application '{ClientId}' is already up to date — skipping update.")]
        public static partial void ApplicationUnchanged(ILogger logger, string clientId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC scope '{ScopeName}'.")]
        public static partial void ScopeCreated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC scope '{ScopeName}'.")]
        public static partial void ScopeUpdated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC scope '{ScopeName}' is already up to date — skipping update.")]
        public static partial void ScopeUnchanged(ILogger logger, string scopeName);
    }
}
