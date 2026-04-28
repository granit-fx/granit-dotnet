using System.Text.RegularExpressions;
using Granit.Dashboards.Templating;
using Microsoft.Extensions.Logging;

namespace Granit.Dashboards.Internal.Templating;

/// <summary>
/// Default <see cref="IVariableSubstituter"/> implementation: pure regex-driven
/// <c>${variable}</c> replacement, no scripting. Unknown variables log a warning
/// and resolve to empty.
/// </summary>
internal sealed partial class DefaultVariableSubstituter(
    ILogger<DefaultVariableSubstituter> logger) : IVariableSubstituter
{
    // ${name} where name is any non-`}` characters. Bounded to avoid backtracking.
    [GeneratedRegex(@"\$\{(?<name>[^}]+)\}", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex VariableRegex();

    public string? Substitute(string? input, IReadOnlyDictionary<string, object?> context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        return VariableRegex().Replace(input, match =>
        {
            string name = match.Groups["name"].Value;
            if (context.TryGetValue(name, out object? value))
            {
                return value?.ToString() ?? string.Empty;
            }

            LogUnknownVariable(name);
            return string.Empty;
        });
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning,
        Message = "Variable '{VariableName}' is not present in the substitution context — replaced with empty.")]
    private partial void LogUnknownVariable(string variableName);
}
