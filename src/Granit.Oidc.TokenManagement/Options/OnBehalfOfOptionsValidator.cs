using Granit.Oidc.ClientAuthentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Oidc.TokenManagement.Options;

/// <summary>
/// Validates <see cref="OnBehalfOfOptions"/> at startup.
/// Refuses any configuration that would reintroduce the confused-deputy
/// vector (no audience, no host allow-list, non-https authority in prod).
/// </summary>
internal sealed class OnBehalfOfOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<OnBehalfOfOptions>
{
    public ValidateOptionsResult Validate(string? name, OnBehalfOfOptions options)
    {
        List<string> failures = [];
        string prefix = string.IsNullOrEmpty(name) ? "OnBehalfOfOptions" : $"OnBehalfOfOptions[{name}]";

        if (string.IsNullOrWhiteSpace(options.Authority))
        {
            failures.Add($"{prefix}.{nameof(options.Authority)} is required.");
        }
        else if (!environment.IsDevelopment()
                 && !options.Authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add(
                $"{prefix}.{nameof(options.Authority)} must be https in non-Development environments. " +
                $"Got '{options.Authority}'.");
        }

        if (string.IsNullOrWhiteSpace(options.ClientId))
        {
            failures.Add($"{prefix}.{nameof(options.ClientId)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add(
                $"{prefix}.{nameof(options.Audience)} is required. Without an audience the " +
                "exchanged token is not narrowed to the downstream API, defeating the purpose " +
                "of token exchange (RFC 8693).");
        }

        if (options.AllowedHosts is null || options.AllowedHosts.Length == 0)
        {
            failures.Add(
                $"{prefix}.{nameof(options.AllowedHosts)} must contain at least one host. " +
                "Without it the handler has no way to detect attacker-influenced target URLs.");
        }

        // At least one form of client authentication must be configured
        bool hasClientSecret = !string.IsNullOrEmpty(options.ClientSecret);
        bool hasSigningKey = !string.IsNullOrEmpty(options.ClientSigningKeyJwk);

        switch (options.ClientAuthenticationMethod)
        {
            case ClientAuthenticationMethod.ClientSecretPost when !hasClientSecret:
                failures.Add(
                    $"{prefix}.{nameof(options.ClientSecret)} is required when " +
                    $"{nameof(options.ClientAuthenticationMethod)} = ClientSecretPost.");
                break;

            case ClientAuthenticationMethod.PrivateKeyJwt when !hasSigningKey:
                failures.Add(
                    $"{prefix}.{nameof(options.ClientSigningKeyJwk)} is required when " +
                    $"{nameof(options.ClientAuthenticationMethod)} = PrivateKeyJwt.");
                break;
        }

        if (options.TokenLifetimeSafetyMargin < TimeSpan.Zero)
        {
            failures.Add(
                $"{prefix}.{nameof(options.TokenLifetimeSafetyMargin)} must be non-negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
