using Granit.Http.ApiDocumentation.Internal;
using Granit.Http.ApiDocumentation.Options;
using Granit.Http.SecurityHeaders;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace Granit.Http.ApiDocumentation.Extensions;

/// <summary>
/// Extensions for enabling Granit OpenAPI endpoints and the Scalar interactive UI.
/// </summary>
public static partial class ApiDocumentationApplicationBuilderExtensions
{
    /// <summary>
    /// Maps OpenAPI JSON endpoints (<c>/openapi/v{n}.json</c>) and the Scalar interactive UI.
    /// Always enabled in Development; in Production only when
    /// <see cref="ApiDocumentationOptions.EnableInProduction"/> is <c>true</c>.
    /// When <see cref="ApiDocumentationOptions.AuthorizationPolicy"/> is set, the endpoints
    /// are protected by the specified policy; when empty, anonymous access is explicitly allowed.
    /// </summary>
    public static WebApplication UseGranitApiDocumentation(this WebApplication app)
    {
        ApiDocumentationOptions options =
            app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

        bool shouldEnable = app.Environment.IsDevelopment() || options.EnableInProduction;
        if (!shouldEnable)
        {
            return app;
        }

        // OpenAPI enumeration is a reconnaissance aid for attackers.
        // If the app explicitly opted into production exposure but left the
        // access policy unset, the endpoints inherit the host's default auth
        // behaviour — which, absent a FallbackPolicy, is anonymous access.
        // Emit a Warning at startup so the misconfiguration is visible in
        // logs even before a real request hits the surface.
        if (!app.Environment.IsDevelopment()
            && options.EnableInProduction
            && options.AuthorizationPolicy is null)
        {
            ILogger logger = app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Granit.Http.ApiDocumentation");
            LogProductionOpenApiWithoutPolicy(logger);
        }

        // Distinct() guards against the .NET configuration binder appending
        // bound values to the default list (e.g. default [1] + config [1] → [1,1]).
        foreach (int majorVersion in options.MajorVersions.Distinct())
        {
            IEndpointConventionBuilder openApiEndpoint =
                app.MapOpenApi($"/openapi/v{majorVersion}.json");
            ApplyAuthorizationPolicy(openApiEndpoint, options.AuthorizationPolicy);
        }

        IEndpointConventionBuilder scalarEndpoint =
            app.MapScalarApiReference(scalarOptions =>
            {
                scalarOptions.WithTitle(options.Title);

                if (!string.IsNullOrEmpty(options.FaviconUrl))
                {
                    scalarOptions.WithFavicon(options.FaviconUrl);
                }

                if (options.OAuth2.IsConfigured)
                {
                    scalarOptions.AddAuthorizationCodeFlow("OAuth2", flow =>
                    {
                        flow
                            .WithClientId(options.OAuth2.ClientId!)
                            .WithSelectedScopes(options.OAuth2.Scopes);

                        if (options.OAuth2.EnablePkce)
                        {
                            flow.WithPkce(Pkce.Sha256);
                        }

                        if (!string.IsNullOrEmpty(options.OAuth2.RedirectUri))
                        {
                            flow.WithRedirectUri(options.OAuth2.RedirectUri);
                        }
                    });
                }
            });
        scalarEndpoint.WithMetadata(new ScalarApiReferenceMetadata());

        // Scalar's Authorize button opens the IdP in a popup and polls the
        // popup's location for the auth code. The framework default COOP
        // ('same-origin') severs the opener↔popup reference the moment the
        // popup navigates cross-origin to the IdP. Mark the endpoint so the
        // security-headers middleware downgrades COOP to 'unsafe-none' for
        // /scalar only; routes without the marker keep the strict baseline.
        // Only needed when OAuth2 is wired — otherwise no popup is opened.
        if (options.OAuth2.IsConfigured)
        {
            scalarEndpoint.WithMetadata(new AllowsPopupAuthorizationMetadata());
        }

        ApplyAuthorizationPolicy(scalarEndpoint, options.AuthorizationPolicy);

        RegisterScalarCspContributor(app);

        return app;
    }

    /// <summary>
    /// Registers <see cref="ScalarCspContributor"/> into the CSP composer's
    /// registry so the strict default CSP is relaxed on the Scalar route.
    /// Resolves the registry from the <b>root</b> application container
    /// (<see cref="IApplicationBuilder.ApplicationServices"/>) — this code
    /// runs at app-build time, before any request scope exists.
    /// </summary>
    /// <remarks>
    /// The registry is resolved via <c>GetService</c>, not
    /// <c>GetRequiredService</c>: <see cref="ICspContributorRegistry"/> lives
    /// in <c>Granit.Http.SecurityHeaders.Abstractions</c>, but its
    /// implementation lives in <c>Granit.Http.SecurityHeaders</c>. If a
    /// consumer references <c>Granit.Http.ApiDocumentation</c> without
    /// pulling in <c>Granit.Http.SecurityHeaders</c>, the registry is
    /// unresolved — the contributor registration is a no-op (logged at
    /// Debug) and the consumer is left to manage CSP externally.
    /// </remarks>
    private static void RegisterScalarCspContributor(WebApplication app)
    {
        ICspContributorRegistry? registry =
            app.Services.GetService<ICspContributorRegistry>();

        if (registry is null)
        {
            ILogger logger = app.Services
                .GetRequiredService<ILoggerFactory>()
                .CreateLogger("Granit.Http.ApiDocumentation");
            LogCspRegistryUnresolved(logger);
            return;
        }

        registry.Add(new ScalarCspContributor(
            app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>()));
    }

    private static void ApplyAuthorizationPolicy(
        IEndpointConventionBuilder endpoint,
        string? policy)
    {
        if (policy is null)
        {
            return;
        }

        if (policy.Length == 0)
        {
            endpoint.AllowAnonymous();
            return;
        }

        endpoint.RequireAuthorization(policy);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "OpenAPI/Scalar is exposed in production with no AuthorizationPolicy configured. " +
                  "The endpoint's access depends on the application's global fallback policy. " +
                  "Set ApiDocumentation:AuthorizationPolicy to a named policy (e.g. \"ApiDocsReaders\") " +
                  "or an empty string (explicit anonymous) to silence this warning.")]
    private static partial void LogProductionOpenApiWithoutPolicy(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "ICspContributorRegistry not registered — Scalar CSP contributor skipped " +
                  "(consumer is managing security headers externally, or " +
                  "Granit.Http.SecurityHeaders is not referenced).")]
    private static partial void LogCspRegistryUnresolved(ILogger logger);
}
