using Granit.Authentication.DPoP.Diagnostics;
using Granit.Authentication.DPoP.Middleware;
using Granit.Authentication.DPoP.Options;
using Granit.Authentication.DPoP.Validation;
using Granit.Authentication.DPoP.Validation.Internal;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.DPoP.Extensions;

/// <summary>
/// Extension methods for adding DPoP proof validation to JWT Bearer authentication.
/// </summary>
public static class DPoPJwtBearerExtensions
{
    /// <summary>
    /// Registers DPoP proof validation services and configures <see cref="JwtBearerOptions"/>
    /// to accept the <c>DPoP</c> authorization scheme alongside <c>Bearer</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration for DPoP validation options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitDPoPValidation(
        this IServiceCollection services,
        Action<DPoPValidationOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddGranitDPoPProofValidator();

        // Configure JwtBearer to accept "DPoP <token>" in addition to "Bearer <token>"
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            JwtBearerEvents? existingEvents = options.Events;

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // Accept "Authorization: DPoP <token>" scheme
                    string? auth = context.Request.Headers.Authorization.ToString();
                    if (!string.IsNullOrEmpty(auth)
                        && auth.StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Token = auth["DPoP ".Length..];
                    }

                    // Chain existing handler if present
                    return existingEvents?.OnMessageReceived?.Invoke(context) ?? Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                    existingEvents?.OnAuthenticationFailed?.Invoke(context) ?? Task.CompletedTask,
                OnTokenValidated = context =>
                    existingEvents?.OnTokenValidated?.Invoke(context) ?? Task.CompletedTask,
                OnChallenge = context =>
                    existingEvents?.OnChallenge?.Invoke(context) ?? Task.CompletedTask,
                OnForbidden = context =>
                    existingEvents?.OnForbidden?.Invoke(context) ?? Task.CompletedTask,
            };
        });

        return services;
    }

    /// <summary>
    /// Adds the DPoP proof validation middleware. Must be called after <c>UseAuthentication()</c>.
    /// Validates DPoP proofs, verifies <c>cnf.jkt</c> token binding, and enforces
    /// DPoP requirement when configured.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UseGranitDPoPValidation(this IApplicationBuilder app) =>
        app.UseMiddleware<DPoPValidationMiddleware>();

    /// <summary>
    /// Registers <see cref="IDPoPProofValidator"/> alone, without configuring the JwtBearer
    /// event pipeline. Useful for hosts that need the validator from a non-JwtBearer code
    /// path — for example the OpenIddict server's <c>DPoPTokenBindingHandler</c>, which
    /// validates the proof presented at <c>/connect/token</c> independently of any
    /// resource-side bearer authentication.
    /// </summary>
    public static IServiceCollection AddGranitDPoPProofValidator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // The validator depends on DPoPValidationMetrics — register it alongside so the
        // helper is fully self-contained, regardless of which entry point the host calls.
        services.TryAddSingleton<DPoPValidationMetrics>();
        services.TryAddSingleton<IDPoPProofValidator, DPoPProofValidator>();
        return services;
    }
}
