using Granit.Http.ApiDocumentation.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;

namespace Granit.Http.ApiDocumentation.Extensions;

/// <summary>
/// Extensions for enabling Granit OpenAPI endpoints and the Scalar interactive UI.
/// </summary>
public static class ApiDocumentationApplicationBuilderExtensions
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
                    });
                }
            });
        ApplyAuthorizationPolicy(scalarEndpoint, options.AuthorizationPolicy);

        return app;
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
}
