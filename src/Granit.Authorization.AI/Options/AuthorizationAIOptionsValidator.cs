using Microsoft.Extensions.Options;

namespace Granit.Authorization.AI.Options;

/// <summary>
/// Validates <see cref="AuthorizationAIOptions"/> at startup to catch
/// misconfigurations that could degrade AI anomaly detection security.
/// </summary>
internal sealed class AuthorizationAIOptionsValidator
    : IValidateOptions<AuthorizationAIOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthorizationAIOptions options)
    {
        List<string>? failures = null;

        if (options.UnavailableRiskScore is < 0.0 or > 1.0)
        {
            (failures ??= []).Add(
                $"UnavailableRiskScore must be between 0.0 and 1.0, got {options.UnavailableRiskScore}.");
        }

        if (options.TimeoutSeconds is < 1 or > 30)
        {
            (failures ??= []).Add(
                $"TimeoutSeconds must be between 1 and 30, got {options.TimeoutSeconds}.");
        }

        if (string.IsNullOrWhiteSpace(options.WorkspaceName))
        {
            (failures ??= []).Add(
                "WorkspaceName must not be empty.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
