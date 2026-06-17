using System.Diagnostics.CodeAnalysis;
using Granit.Authentication.DPoP.Extensions;
using Granit.Authentication.Extensions;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

#pragma warning disable GRSEC003 // OpenIddict permission/grant type constants, not secrets

namespace Granit.OpenIddict.Server.Extensions;

/// <summary>
/// Extension methods for registering the OpenIddict OIDC server and local validation.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictServerHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the OpenIddict server (authorization endpoints, flows, signing keys,
    /// custom grant types) and local token validation.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictServer(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Read options from configuration for build-time decisions
        GranitOpenIddictOptions granitOptions = new();
        builder.Configuration.GetSection(GranitOpenIddictOptions.SectionName).Bind(granitOptions);

        // FAPI 2.0 profile: apply all mandatory server-side constraints
        if (granitOptions.EnableFapi2Profile)
        {
            granitOptions.WithFapi2Profile();
        }

        // Entity caching uses ClientId as sole cache key — incompatible with multi-tenancy
        // (two tenants with the same ClientId would share cached data → cross-tenant leak).
        if (granitOptions.EnableEntityCaching)
        {
            bool hasMultiTenancy = builder.Services.Any(
                s => s.ServiceType.FullName == "Granit.MultiTenancy.ICurrentTenant");
            if (hasMultiTenancy)
            {
                throw new InvalidOperationException(
                    "OpenIddict entity caching is incompatible with multi-tenancy. "
                    + "OpenIddict uses ClientId as cache key, which causes cross-tenant data pollution. "
                    + "Set EnableEntityCaching = false (default) or remove multi-tenancy.");
            }
        }

        // The DPoP token-binding event handler (registered below via
        // DPoPTokenBindingHandler.Descriptor) requires IDPoPProofValidator. Register it
        // here so the OpenIddict server is self-contained — hosts that also call
        // AddGranitDPoPValidation() on the resource side benefit from the same
        // TryAddSingleton; without this, the OIDC server fails at sign-in time when no
        // resource-side DPoP validation is registered (e.g. dedicated authorization-server
        // deployments and integration tests).
        builder.Services.AddGranitDPoPProofValidator();

        OpenIddictBuilder openIddict = builder.Services.AddOpenIddict();

        // ──── Server — OIDC authorization server ────
        openIddict.AddServer(options =>
        {
            // ──── Endpoints ────
            options
                .SetAuthorizationEndpointUris("/connect/authorize")
                .SetTokenEndpointUris("/connect/token")
                .SetUserInfoEndpointUris("/connect/userinfo")
                .SetIntrospectionEndpointUris("/connect/introspect")
                .SetRevocationEndpointUris("/connect/revoke")
                .SetEndSessionEndpointUris("/connect/logout")
                .SetDeviceAuthorizationEndpointUris("/connect/device")
                .SetEndUserVerificationEndpointUris("/connect/verify")
                .SetPushedAuthorizationEndpointUris("/connect/par");

            // ──── Flows ────
            options
                .AllowAuthorizationCodeFlow()
                .AllowClientCredentialsFlow()
                .AllowRefreshTokenFlow()
                .AllowDeviceAuthorizationFlow();

            // Token Exchange (RFC 8693) — microservice delegation/impersonation
            if (granitOptions.EnableTokenExchange)
            {
                options.AllowTokenExchangeFlow();
            }

            // PKCE required by default (can be relaxed per-application)
            options.RequireProofKeyForCodeExchange();

            // PAR enforcement (optional — endpoint is always available via SetPushedAuthorizationEndpointUris)
            if (granitOptions.RequirePar)
            {
                options.RequirePushedAuthorizationRequests();
            }

            // JAR enforcement (RFC 9101): reject unsigned authorization requests.
            // Mandatory for FAPI 2.0 (set automatically via WithFapi2Profile).
            // OpenIddict 7.x does not expose a dedicated builder method; enforced via a
            // custom ValidateAuthorizationRequest event handler that rejects requests
            // missing a signed 'request' JWT parameter.
            if (granitOptions.RequireJar)
            {
                options.AddEventHandler(
                    OpenIddictServerHandlerDescriptor
                        .CreateBuilder<ValidateAuthorizationRequestContext>()
                        .UseInlineHandler((ValidateAuthorizationRequestContext ctx) =>
                        {
                            // The 'request' parameter carries the JAR object (RFC 9101 §4).
                            if (string.IsNullOrEmpty((string?)ctx.Request["request"]))
                            {
                                ctx.Reject(
                                    error: OpenIddictConstants.Errors.InvalidRequest,
                                    description: "A JWT-secured authorization request object is required (JAR, RFC 9101).");
                            }
                            return default;
                        })
                        .SetOrder(int.MinValue + 100)
                        .SetType(OpenIddictServerHandlerType.Custom)
                        .Build());
            }

            // ──── FAPI 2.0 hardening ────
            if (granitOptions.EnableFapi2Profile)
            {
                // Authorization code lifetime ≤ 60s (FAPI 2.0 §5.3.2.1)
                options.SetAuthorizationCodeLifetime(TimeSpan.FromSeconds(60));

                // Access token lifetime ≤ 10 min (FAPI 2.0 tight token binding)
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
            }

            // ──── Signing & encryption ────
            // Ephemeral keys: regenerated at every process start. Only allowed in
            // Development OR when explicitly opted in via AllowEphemeralKeys = true
            // (typically for unit tests). Production deployments MUST replace with
            // options.AddSigningCertificate() / options.AddEncryptionCertificate()
            // or load keys from Vault via Granit.Vault.HashiCorp.
            bool ephemeralAllowed = builder.Environment.IsDevelopment()
                || granitOptions.AllowEphemeralKeys;
            if (!ephemeralAllowed)
            {
                throw new InvalidOperationException(
                    "OpenIddict ephemeral signing/encryption keys are forbidden outside Development. " +
                    "Configure persistent keys (AddSigningCertificate / AddEncryptionCertificate) " +
                    "or set GranitOpenIddictOptions.AllowEphemeralKeys = true at your own risk.");
            }
            options
                .AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();

            // ──── Issuer ────
            if (granitOptions.Issuer is not null)
            {
                options.SetIssuer(granitOptions.Issuer);
            }

            // ──── Token formats ────
            options.DisableAccessTokenEncryption();

            // Reference tokens: opaque tokens validated against DB on every request.
            // Enables instant revocation at the cost of +1 DB round-trip per API call.
            if (granitOptions.UseReferenceTokens)
            {
                options.UseReferenceAccessTokens();
                options.UseReferenceRefreshTokens();
            }

            // ──── ASP.NET Core integration ────
            OpenIddictServerAspNetCoreBuilder aspNetCore = options
                .UseAspNetCore()
                .EnableAuthorizationEndpointPassthrough()
                .EnableTokenEndpointPassthrough()
                .EnableUserInfoEndpointPassthrough()
                .EnableEndSessionEndpointPassthrough()
                .EnableEndUserVerificationEndpointPassthrough()
                .EnableStatusCodePagesIntegration();

            if (builder.Environment.IsDevelopment())
            {
                aspNetCore.DisableTransportSecurityRequirement();
            }

            // ──── Custom grant types ────
            options.AllowCustomFlow("urn:granit:grant_type:two_factor");
            options.AllowCustomFlow("urn:granit:grant_type:passkey");

            // ──── Scopes ────
            options.RegisterScopes(
                OpenIddictConstants.Scopes.Email,
                OpenIddictConstants.Scopes.Profile,
                OpenIddictConstants.Scopes.Roles,
                "offline_access");

            // ──── Custom event handlers ────
            // Enforces the MultiTenancySides policy declared on each OIDC application
            // at sign-in: host-only clients reject tenant users, tenant-only clients
            // reject host users. See ClientSideAuthorizationHandler for semantics.
            options.AddEventHandler(ClientSideAuthorizationHandler.Descriptor);

            // Validates the DPoP proof presented at /connect/token and stamps the
            // resulting JWK Thumbprint as the cnf.jkt confirmation claim on the issued
            // access token (RFC 9449 §6). In FAPI 2.0, a missing DPoP header rejects
            // the request — see DPoPTokenBindingHandler for semantics.
            options.AddEventHandler(DPoPTokenBindingHandler.Descriptor);

            // Announces a new user session (UserSessionCreatedEto) when the token endpoint
            // issues a refresh token, so consumers (anomaly detection, geo, notifications)
            // react out-of-band. Ordered after token generation and the reject-capable
            // handlers above — see OpenIddictUserSessionCreatedHandler for semantics.
            options.AddEventHandler(OpenIddictUserSessionCreatedHandler.Descriptor);
        });

        // ──── Validation — token validation for resource servers ────
        openIddict.AddValidation(options =>
        {
            options.UseLocalServer();
            options.UseAspNetCore();

            // OpenIddict has no DPoP support; its built-in extractor only accepts "Bearer".
            // Granit owns the whole DPoP chain, so this handler picks up
            // "Authorization: DPoP <token>" (RFC 9449 §7.1) for every DPoP client — BFF
            // sessions as well as no-BFF consumers (SPA, mobile, native) where mTLS is
            // impractical. It is a permanent part of the integration, not a stopgap.
            options.AddEventHandler(DPoPValidationTokenExtractionHandler.Descriptor);
        });

        // Normalize OIDC short-name "role" claims emitted by OpenIddict.Validation into
        // ClaimTypes.Role so PermissionChecker.AdminRoles bypass and ICurrentUserService
        // .GetRoles() agree with ClaimsPrincipal.IsInRole(). Mirrors what JwtBearer does
        // implicitly via TokenValidationParameters.RoleClaimType. Scheme names kept as
        // string literals so this package does not depend on
        // OpenIddict.Validation.AspNetCore solely to reference its constant.
        //
        // Two schemes are registered because UseLocalServer() (self-hosted: server and
        // validation co-located, the configuration this method always produces) returns
        // a ClaimsPrincipal whose ClaimsIdentity.AuthenticationType is the default
        // "AuthenticationTypes.Federation" rather than the validation handler's scheme
        // name. Without "AuthenticationTypes.Federation" in the scheme list, the
        // transformation gates out, the short "role" claim is never copied to
        // ClaimTypes.Role, ICurrentUserService.GetRoles() returns empty, and every
        // role-gated permission check returns 403.
        builder.Services.AddGranitRoleClaimNormalization(o =>
        {
            const string openIddictValidationScheme = "OpenIddict.Validation.AspNetCore";
            const string federationScheme = "AuthenticationTypes.Federation";

            if (!o.Schemes.Contains(openIddictValidationScheme))
            {
                o.Schemes.Add(openIddictValidationScheme);
            }

            if (!o.Schemes.Contains(federationScheme))
            {
                o.Schemes.Add(federationScheme);
            }
        });

        return builder;
    }
}

#pragma warning restore GRSEC003
