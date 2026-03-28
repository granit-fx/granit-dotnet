using Microsoft.Extensions.Options;

namespace Granit.Templating.AI.Options;

/// <summary>
/// Validates <see cref="TemplatingAIOptions"/> at startup to catch
/// misconfigurations that would silently degrade AI template generation.
/// </summary>
internal sealed class TemplatingAIOptionsValidator : IValidateOptions<TemplatingAIOptions>
{
    public ValidateOptionsResult Validate(string? name, TemplatingAIOptions options)
    {
        List<string> failures = [];

        if (string.IsNullOrWhiteSpace(options.WorkspaceName))
        {
            failures.Add(
                "WorkspaceName must not be empty — it identifies the AI workspace for template generation.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            failures.Add(
                $"TimeoutSeconds must be a positive integer, got {options.TimeoutSeconds}.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
