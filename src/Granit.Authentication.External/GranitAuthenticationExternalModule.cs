using Granit.Authentication.External.Extensions;
using Granit.Authentication.External.Options;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.External;

/// <summary>
/// Granit module for external/social authentication. Binds <see cref="ExternalAuthOptions"/> and
/// fails fast at startup on a configured provider that has no registered authentication handler.
/// </summary>
/// <remarks>
/// This module owns the abstractions only; the actual schemes are registered by the per-provider
/// packages (<c>Granit.Authentication.External.Google</c>, …) whose modules depend on this one.
/// </remarks>
public sealed class GranitAuthenticationExternalModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitExternalProviders();

    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Every provider listed under Authentication:External:Providers is advertised to users as
        // available, so each MUST have a registered authentication handler. A provider without its
        // scheme (host forgot to reference/enable the matching Granit.Authentication.External.<X>
        // package) would otherwise only fail when a user clicks "Sign in with X".
        ExternalAuthOptions options = context.ServiceProvider
            .GetRequiredService<IOptions<ExternalAuthOptions>>().Value;

        if (options.Providers.Count == 0)
        {
            return;
        }

        IAuthenticationSchemeProvider schemeProvider = context.ServiceProvider
            .GetRequiredService<IAuthenticationSchemeProvider>();

        var registeredSchemes = schemeProvider.GetAllSchemesAsync()
            .GetAwaiter().GetResult()
            .Select(s => s.Name)
            .ToHashSet(StringComparer.Ordinal);

        string[] missing = options.Providers
            .Select(p => p.SchemeName)
            .Where(name => !registeredSchemes.Contains(name))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"External authentication provider(s) [{string.Join(", ", missing)}] are configured "
                + $"under '{ExternalAuthOptions.SectionName}:Providers' but have no registered "
                + "authentication handler. Reference the matching Granit.Authentication.External.<Provider> "
                + "package (and add its module via [DependsOn]) so its scheme is registered, or remove "
                + "the provider from configuration.");
        }
    }
}
