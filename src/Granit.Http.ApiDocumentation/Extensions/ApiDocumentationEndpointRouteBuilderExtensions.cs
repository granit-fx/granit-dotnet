using Granit.Http.ApiDocumentation.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Http.ApiDocumentation.Extensions;

/// <summary>
/// Extensions for mapping the Granit OpenAPI JSON endpoints without any UI stack.
/// </summary>
public static class ApiDocumentationEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps one OpenAPI JSON endpoint per configured major version
    /// (<c>/openapi/v{n}.json</c> for each entry in
    /// <see cref="ApiDocumentationOptions.MajorVersions"/>). Headless by design —
    /// no interactive UI, no CSP machinery. The <c>Granit.Http.ApiDocumentation.Scalar</c>
    /// package's <c>UseGranitApiDocumentation()</c> calls this and layers the Scalar UI
    /// (with its own environment gating and authorization policy) on top.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="configureEndpoint">
    /// Optional per-endpoint convention hook (e.g. <c>e => e.RequireAuthorization("Docs")</c>),
    /// applied to each mapped OpenAPI JSON endpoint.
    /// </param>
    public static WebApplication MapGranitOpenApiDocuments(
        this WebApplication app,
        Action<IEndpointConventionBuilder>? configureEndpoint = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        ApiDocumentationOptions options =
            app.Services.GetRequiredService<IOptions<ApiDocumentationOptions>>().Value;

        // Distinct() guards against the .NET configuration binder appending
        // bound values to the default list (e.g. default [1] + config [1] → [1,1]).
        foreach (int majorVersion in options.MajorVersions.Distinct())
        {
            IEndpointConventionBuilder openApiEndpoint =
                app.MapOpenApi($"/openapi/v{majorVersion}.json");
            configureEndpoint?.Invoke(openApiEndpoint);
        }

        return app;
    }
}
