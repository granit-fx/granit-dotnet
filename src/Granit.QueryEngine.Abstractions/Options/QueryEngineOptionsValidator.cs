using Microsoft.Extensions.Options;

namespace Granit.QueryEngine.Options;

/// <summary>
/// Validates <see cref="QueryEngineOptions"/> so misconfiguration fails at startup
/// (with <c>ValidateOnStart</c>) or at first resolution — never per request. In
/// particular a malformed <see cref="QueryEngineOptions.CursorHmacKey"/> used to
/// surface as a <see cref="FormatException"/> inside the first query execution.
/// </summary>
internal sealed class QueryEngineOptionsValidator : IValidateOptions<QueryEngineOptions>
{
    private const int MinHmacKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, QueryEngineOptions options)
    {
        List<string> failures = [];

        if (options.DefaultPageSize <= 0)
        {
            failures.Add($"DefaultPageSize must be positive, got {options.DefaultPageSize}.");
        }

        if (options.MaxPageSize <= 0)
        {
            failures.Add($"MaxPageSize must be positive, got {options.MaxPageSize}.");
        }

        if (options.DefaultPageSize > 0 && options.MaxPageSize > 0
            && options.DefaultPageSize > options.MaxPageSize)
        {
            failures.Add(
                $"DefaultPageSize ({options.DefaultPageSize}) must not exceed MaxPageSize ({options.MaxPageSize}).");
        }

        if (options.MaxStreamSize <= 0)
        {
            failures.Add($"MaxStreamSize must be positive, got {options.MaxStreamSize}.");
        }

        if (options.MaxGroupCount <= 0)
        {
            failures.Add($"MaxGroupCount must be positive, got {options.MaxGroupCount}.");
        }

        if (!string.IsNullOrEmpty(options.CursorHmacKey))
        {
            try
            {
                byte[] key = Convert.FromBase64String(options.CursorHmacKey);
                if (key.Length < MinHmacKeyBytes)
                {
                    failures.Add(
                        $"CursorHmacKey must decode to at least {MinHmacKeyBytes} bytes (256 bits) " +
                        $"for HMAC-SHA256 cursor signing, got {key.Length} bytes.");
                }
            }
            catch (FormatException)
            {
                failures.Add("CursorHmacKey must be a valid Base64 string.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
