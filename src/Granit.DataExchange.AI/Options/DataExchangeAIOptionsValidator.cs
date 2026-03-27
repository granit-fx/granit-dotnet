using Microsoft.Extensions.Options;

namespace Granit.DataExchange.AI.Options;

/// <summary>
/// Validates <see cref="DataExchangeAIOptions"/> at startup to catch
/// misconfigurations that would silently degrade AI mapping security.
/// </summary>
internal sealed class DataExchangeAIOptionsValidator : IValidateOptions<DataExchangeAIOptions>
{
    public ValidateOptionsResult Validate(string? name, DataExchangeAIOptions options)
    {
        List<string>? failures = null;

        if (string.IsNullOrWhiteSpace(options.WorkspaceName))
        {
            (failures ??= []).Add(
                "WorkspaceName must not be empty — it identifies the AI workspace for mapping suggestions.");
        }

        if (options.TimeoutSeconds <= 0)
        {
            (failures ??= []).Add(
                $"TimeoutSeconds must be a positive integer, got {options.TimeoutSeconds}.");
        }

        if (options.MinConfidenceScore is < 0.0 or > 1.0)
        {
            (failures ??= []).Add(
                $"MinConfidenceScore must be between 0.0 and 1.0, got {options.MinConfidenceScore}.");
        }

        if (options.PreviewRowCount <= 0)
        {
            (failures ??= []).Add(
                $"PreviewRowCount must be a positive integer, got {options.PreviewRowCount}.");
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
