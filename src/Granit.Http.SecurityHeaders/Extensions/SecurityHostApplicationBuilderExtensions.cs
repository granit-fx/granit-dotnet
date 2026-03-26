using Granit.Http.SecurityHeaders.Options;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Http.SecurityHeaders.Extensions;

/// <summary>
/// Extension methods for registering Granit HTTP security hardening services.
/// </summary>
public static class SecurityHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds HTTP security hardening for Granit applications.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///   <item>
    ///     Suppresses the Kestrel <c>Server</c> response header
    ///     (<see cref="KestrelServerOptions.AddServerHeader"/> = <c>false</c>).
    ///   </item>
    ///   <item>
    ///     Configures HSTS with OWASP recommended defaults (1 year, includeSubDomains).
    ///   </item>
    ///   <item>
    ///     Registers <see cref="GranitSecurityHeadersOptions"/> from the
    ///     <c>"SecurityHeaders"</c> configuration section.
    ///   </item>
    /// </list>
    /// <para>
    /// Call <see cref="SecurityApplicationBuilderExtensions.UseGranitSecurityHeaders"/>
    /// in the middleware pipeline to inject response headers (X-Content-Type-Options,
    /// X-Frame-Options, Referrer-Policy, Permissions-Policy, COOP, CORP).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitHttpSecurity(
        this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<GranitSecurityHeadersOptions>()
            .BindConfiguration(GranitSecurityHeadersOptions.SectionName);

        builder.Services.AddSingleton<IValidateOptions<GranitSecurityHeadersOptions>,
            GranitSecurityHeadersOptionsValidator>();

        builder.Services.AddSingleton<IConfigureOptions<KestrelServerOptions>,
            ConfigureKestrelServerOptions>();

        builder.Services.AddSingleton<IConfigureOptions<HstsOptions>,
            ConfigureHstsOptions>();

        return builder;
    }

    private sealed class ConfigureKestrelServerOptions(
        IOptions<GranitSecurityHeadersOptions> securityOptions)
        : IConfigureOptions<KestrelServerOptions>
    {
        public void Configure(KestrelServerOptions options) =>
            options.AddServerHeader = !securityOptions.Value.SuppressServerHeader;
    }

    private sealed class ConfigureHstsOptions(
        IOptions<GranitSecurityHeadersOptions> securityOptions)
        : IConfigureOptions<HstsOptions>
    {
        public void Configure(HstsOptions options)
        {
            GranitSecurityHeadersOptions config = securityOptions.Value;

            if (!config.EnableHsts)
            {
                return;
            }

            options.MaxAge = TimeSpan.FromSeconds(config.HstsMaxAgeSeconds);
            options.IncludeSubDomains = config.HstsIncludeSubDomains;
            options.Preload = config.HstsPreload;
        }
    }
}
