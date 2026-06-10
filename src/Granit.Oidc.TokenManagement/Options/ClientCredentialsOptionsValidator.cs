using Granit.Oidc.ClientAuthentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Oidc.TokenManagement.Options;

/// <summary>
/// Validates <see cref="ClientCredentialsOptions"/> when first resolved.
/// Catches misconfigurations that would otherwise fail silently at the first
/// outbound request: missing authority/client id, non-https authority in
/// non-Development environments, and a client-authentication method without its
/// matching credential.
/// </summary>
internal sealed class ClientCredentialsOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<ClientCredentialsOptions>
{
    public ValidateOptionsResult Validate(string? name, ClientCredentialsOptions options)
    {
        List<string> failures = [];
        string prefix = string.IsNullOrEmpty(name) ? "ClientCredentialsOptions" : $"ClientCredentialsOptions[{name}]";

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

        if (options.CacheMargin is { } margin && margin < TimeSpan.Zero)
        {
            failures.Add($"{prefix}.{nameof(options.CacheMargin)} must be non-negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
