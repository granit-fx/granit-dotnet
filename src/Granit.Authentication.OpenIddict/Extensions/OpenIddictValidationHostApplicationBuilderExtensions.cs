using System.Diagnostics.CodeAnalysis;
using Granit.Authentication.OpenIddict.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Authentication.OpenIddict.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict token validation on resource servers.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictValidationHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers OpenIddict remote token validation. Validates JWT/reference tokens
    /// from a remote Granit OpenIddict server via the discovery endpoint.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictAuthentication(
        this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        GranitOpenIddictValidationOptions validationOptions = new();
        builder.Configuration
            .GetSection(GranitOpenIddictValidationOptions.SectionName)
            .Bind(validationOptions);

        builder.Services.AddOpenIddict()
            .AddValidation(options =>
            {
                if (validationOptions.Issuer is not null)
                {
                    options.SetIssuer(validationOptions.Issuer);
                }

                if (!string.IsNullOrEmpty(validationOptions.Audience))
                {
                    options.AddAudiences(validationOptions.Audience);
                }

                options.UseSystemNetHttp();
                options.UseAspNetCore();
            });

        return builder;
    }
}
