using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Oidc;

/// <summary>
/// Registers a generic OpenID Connect authentication handler (authorization-code + PKCE) for each
/// <c>Authentication:External:Providers</c> entry of type <c>Oidc</c>. Set <c>Authority</c> to the
/// issuer URL.
/// </summary>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalOidcModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Builder!.Configuration,
            "Oidc",
            static (builder, provider) => builder.AddOpenIdConnect(provider.SchemeName, options =>
            {
                options.Authority = provider.Authority;
                options.ClientId = provider.ClientId;
                options.ClientSecret = provider.ClientSecret;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.SaveTokens = true;

                if (!string.IsNullOrWhiteSpace(provider.CallbackPath))
                {
                    options.CallbackPath = provider.CallbackPath;
                }

                if (provider.Scopes.Length > 0)
                {
                    options.Scope.Clear();
                    foreach (string scope in provider.Scopes)
                    {
                        options.Scope.Add(scope);
                    }
                }
            }));
}
