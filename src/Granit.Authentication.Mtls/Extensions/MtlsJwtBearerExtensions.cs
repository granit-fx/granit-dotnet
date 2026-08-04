using Granit.Authentication.Mtls.Diagnostics;
using Granit.Authentication.Mtls.Middleware;
using Granit.Authentication.Mtls.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.Mtls.Extensions;

/// <summary>
/// Extension methods for adding mutual-TLS certificate-bound token validation (RFC 8705) to a
/// resource server.
/// </summary>
public static class MtlsJwtBearerExtensions
{
    /// <summary>
    /// Registers mutual-TLS certificate-bound token validation services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for <see cref="MtlsValidationOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitMtlsValidation(
        this IServiceCollection services,
        Action<MtlsValidationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<MtlsValidationOptions> options = services
            .AddOptions<MtlsValidationOptions>()
            .BindConfiguration(MtlsValidationOptions.SectionName);

        // Registered after the configuration binding so an explicit delegate wins over appsettings.
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<MtlsValidationMetrics>();
        return services;
    }

    /// <summary>
    /// Adds the mutual-TLS validation middleware. Must be called after <c>UseAuthentication()</c>.
    /// Verifies that the presented client certificate matches a certificate-bound token's
    /// <c>cnf.x5t#S256</c> confirmation claim.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitMtlsValidation(this IApplicationBuilder app) =>
        app.UseMiddleware<MtlsValidationMiddleware>();
}
