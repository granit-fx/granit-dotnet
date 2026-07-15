using Granit.Domain;
using Granit.Identity;
using Granit.OpenIddict.Extensions;
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

        object entity;
        if (existing is null)
        {
            var appDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = descriptor.ClientId,
                ClientSecret = descriptor.ClientSecret,
            };

            PopulateDescriptor(appDescriptor, descriptor);

            entity = await applicationManager.CreateAsync(appDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ApplicationCreated(logger, descriptor.ClientId);
        }
        else
        {
            entity = existing;
            var appDescriptor = new OpenIddictApplicationDescriptor();
            await applicationManager.PopulateAsync(appDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            // Skip the descriptor update when values are already up to date — prevents
            // ConcurrencyException when migrator and API both run seeders concurrently at
            // startup. Tenant reconciliation below still runs (it is a separate write).
            if (ApplicationNeedsUpdate(appDescriptor, descriptor))
            {
                PopulateDescriptor(appDescriptor, descriptor);

                await applicationManager.UpdateAsync(existing, appDescriptor, cancellationToken)
                    .ConfigureAwait(false);
                Log.ApplicationUpdated(logger, descriptor.ClientId);
            }
            else
            {
                Log.ApplicationUnchanged(logger, descriptor.ClientId);
            }
        }

        await StampTenantAsync(entity, descriptor.TenantId, descriptor.ClientId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reconciles the owning tenant on a seeded application. The tenant is not an OpenIddict
    /// descriptor field, and OpenIddict entities are not audited, so the persistence interceptor
    /// never assigns their <c>TenantId</c> — stamp it directly. <see langword="null"/> = global.
    /// No-op (no write) when the entity already carries the desired tenant.
    /// </summary>
    private async Task StampTenantAsync(
        object entity, Guid? tenantId, string clientId, CancellationToken cancellationToken)
    {
        if (entity is IMultiTenant multiTenant && multiTenant.TenantId != tenantId)
        {
            multiTenant.TenantId = tenantId;
            await applicationManager.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            Log.ApplicationTenantStamped(logger, clientId, tenantId);
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

        if ((current.ApplicationType ?? OpenIddictConstants.ApplicationTypes.Web) !=
            (desired.ApplicationType ?? OpenIddictConstants.ApplicationTypes.Web))
        {
            return true;
        }

        if (current.GetClientSide() != desired.ClientSide)
        {
            return true;
        }

        // SetDeviceKind stores null and Unknown identically (the key is removed), so normalise the desired
        // value before comparing against the stored one — otherwise Unknown would read back as a phantom diff.
        DeviceKind? desiredKind = desired.DeviceKind is null or DeviceKind.Unknown ? null : desired.DeviceKind;
        return current.GetDeviceKind() != desiredKind;
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

        appDescriptor.ApplicationType = source.ApplicationType ?? OpenIddictConstants.ApplicationTypes.Web;

        // Consent type (default: implicit — auto-grant for first-party apps)
        appDescriptor.ConsentType = source.ConsentType ?? OpenIddictConstants.ConsentTypes.Implicit;

        // Host/tenant policy — persisted in Properties so the OIDC server can enforce
        // host-only / tenant-only client access without depending on Granit.Bff.
        appDescriptor.SetClientSide(source.ClientSide);

        // Declared device kind — persisted in Properties and read by the session adapters so /devices
        // classifies devices accurately instead of guessing from the User-Agent.
        appDescriptor.SetDeviceKind(source.DeviceKind);
    }

    private async Task SeedScopeAsync(
        OidcScopeSeedDescriptor descriptor, CancellationToken cancellationToken)
    {
        object? existing = await scopeManager.FindByNameAsync(descriptor.Name, cancellationToken)
            .ConfigureAwait(false);

        object entity;
        if (existing is null)
        {
            var scopeDescriptor = new OpenIddictScopeDescriptor
            {
                Name = descriptor.Name,
                DisplayName = descriptor.DisplayName,
                Description = descriptor.Description,
            };

            foreach (string resource in descriptor.Resources)
            {
                scopeDescriptor.Resources.Add(resource);
            }

            entity = await scopeManager.CreateAsync(scopeDescriptor, cancellationToken).ConfigureAwait(false);
            Log.ScopeCreated(logger, descriptor.Name);
        }
        else
        {
            entity = existing;
            var scopeDescriptor = new OpenIddictScopeDescriptor();
            await scopeManager.PopulateAsync(scopeDescriptor, existing, cancellationToken)
                .ConfigureAwait(false);

            // Skip the descriptor update when values are already up to date — prevents
            // ConcurrencyException when migrator and API both run seeders concurrently at
            // startup. Tenant reconciliation below still runs (it is a separate write).
            if (scopeDescriptor.DisplayName == descriptor.DisplayName
                && scopeDescriptor.Description == descriptor.Description
                && scopeDescriptor.Resources.SetEquals(descriptor.Resources))
            {
                Log.ScopeUnchanged(logger, descriptor.Name);
            }
            else
            {
                scopeDescriptor.DisplayName = descriptor.DisplayName;
                scopeDescriptor.Description = descriptor.Description;

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

        // Reconcile the owning tenant (not an OpenIddict descriptor field; entities are not
        // audited). null = global — the standard OIDC scopes always resolve here.
        if (entity is IMultiTenant multiTenant && multiTenant.TenantId != descriptor.TenantId)
        {
            multiTenant.TenantId = descriptor.TenantId;
            await scopeManager.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
            Log.ScopeTenantStamped(logger, descriptor.Name, descriptor.TenantId);
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

        [LoggerMessage(Level = LogLevel.Debug, Message = "Stamped OIDC application '{ClientId}' with tenant '{TenantId}'.")]
        public static partial void ApplicationTenantStamped(ILogger logger, string clientId, Guid? tenantId);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created OIDC scope '{ScopeName}'.")]
        public static partial void ScopeCreated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Updated OIDC scope '{ScopeName}'.")]
        public static partial void ScopeUpdated(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "OIDC scope '{ScopeName}' is already up to date — skipping update.")]
        public static partial void ScopeUnchanged(ILogger logger, string scopeName);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Stamped OIDC scope '{ScopeName}' with tenant '{TenantId}'.")]
        public static partial void ScopeTenantStamped(ILogger logger, string scopeName, Guid? tenantId);
    }
}
