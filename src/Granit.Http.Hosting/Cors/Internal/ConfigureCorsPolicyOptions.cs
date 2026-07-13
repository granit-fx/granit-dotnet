using Granit.Http.Hosting.Cors.Options;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;

namespace Granit.Http.Hosting.Cors.Internal;

/// <summary>
/// Bridges <see cref="GranitCorsOptions"/> to ASP.NET Core <see cref="CorsOptions"/>
/// by configuring the default CORS policy.
/// </summary>
internal sealed class ConfigureCorsPolicyOptions(
    IOptions<GranitCorsOptions> granitCorsOptions) : IConfigureOptions<CorsOptions>
{
    /// <inheritdoc/>
    public void Configure(CorsOptions options)
    {
        GranitCorsOptions granitOptions = granitCorsOptions.Value;

        options.AddDefaultPolicy(policy =>
        {
            if (granitOptions.AllowedOrigins.Contains("*"))
            {
                policy.AllowAnyOrigin(); // NOSONAR S5122 - intentional: wildcard origin is configuration-driven (GranitCorsOptions), only enabled when explicitly set by the application (typically dev/staging)
            }
            else
            {
                // Use NormalizedOrigins so an entry like "https://app.x.com/"
                // still matches the browser's "Origin: https://app.x.com" header.
                policy.WithOrigins(granitOptions.NormalizedOrigins);
            }

            policy.AllowAnyHeader();
            policy.AllowAnyMethod();

            if (granitOptions.AllowCredentials)
            {
                policy.AllowCredentials();
            }
        });
    }
}
