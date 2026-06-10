using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Apple;

/// <summary>
/// Registers a Sign in with Apple authentication handler for each
/// <c>Authentication:External:Providers</c> entry of type <c>Apple</c>.
/// </summary>
/// <remarks>
/// Apple's client secret is a short-lived JWT signed with a P8 private key, so configure via
/// <see cref="Options.ExternalAuthProvider.Properties"/>: <c>TeamId</c>, <c>KeyId</c>, and
/// <c>PrivateKey</c> (the PEM contents of the .p8). <c>ClientId</c> is the Apple Services ID.
/// </remarks>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalAppleModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Configuration,
            "Apple",
            static (builder, provider) => builder.AddApple(provider.SchemeName, options =>
            {
                options.ClientId = provider.ClientId;
                options.SignInScheme = IdentityConstants.ExternalScheme;

                if (provider.Properties.TryGetValue("TeamId", out string? teamId) && teamId is not null)
                {
                    options.TeamId = teamId;
                }

                if (provider.Properties.TryGetValue("KeyId", out string? keyId) && keyId is not null)
                {
                    options.KeyId = keyId;
                }

                provider.ApplyCallbackAndScopes(path => options.CallbackPath = path, options.Scope);

                // Apple's client secret is a JWT signed with the P8 private key (PEM in config).
                options.GenerateClientSecret = true;
                if (provider.Properties.TryGetValue("PrivateKey", out string? pem)
                    && !string.IsNullOrWhiteSpace(pem))
                {
                    options.PrivateKey = (_, _) => Task.FromResult(pem.AsMemory());
                }
            }));
}
