using Microsoft.Extensions.Options;

namespace Granit.Authorization.AI.Options;

/// <summary>
/// Validates <see cref="AuthorizationAIOptions"/> at startup (fail-fast on misconfiguration).
/// </summary>
internal sealed class AuthorizationAIOptionsValidator : IValidateOptions<AuthorizationAIOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AuthorizationAIOptions options)
    {
        if (options.TimeoutSeconds <= 0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.TimeoutSeconds)} must be positive.");
        }

        if (options.UnavailableRiskScore is < 0.0 or > 1.0)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.UnavailableRiskScore)} must be between 0.0 and 1.0.");
        }

        return ValidateOptionsResult.Success;
    }
}
