using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.AI.Options;

/// <summary>
/// Validates <see cref="QueryEngineAIOptions"/> at startup to catch
/// misconfigurations that would silently degrade NLQ translation.
/// </summary>
internal sealed class QueryEngineAIOptionsValidator : IValidateOptions<QueryEngineAIOptions>
{
    public ValidateOptionsResult Validate(string? name, QueryEngineAIOptions options)
    {
        List<string>? failures = null;

        if (string.IsNullOrWhiteSpace(options.WorkspaceName))
        {
            (failures ??= []).Add(
                "WorkspaceName must not be empty — it identifies the AI workspace for NLQ translation.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            (failures ??= []).Add(
                $"TimeoutSeconds must be a positive integer, got {options.TimeoutSeconds}.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
