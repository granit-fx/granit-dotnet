using Microsoft.Extensions.Options;

namespace Granit.AI.Ollama.Options;

/// <summary>
/// Validates <see cref="OllamaProviderOptions"/> at startup (fail-fast on invalid configuration).
/// </summary>
internal sealed class OllamaProviderOptionsValidator : IValidateOptions<OllamaProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OllamaProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Endpoint)} must be non-empty. " +
                "Set it to the Ollama server URL (e.g. http://localhost:11434).");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Endpoint)} must be a valid HTTP or HTTPS URI. " +
                $"Got: '{options.Endpoint}'.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.DefaultModel)} must be non-empty.");
        }

        if (options.AllowedModels.Count > 0 &&
            !options.AllowedModels.Contains(options.DefaultModel, StringComparer.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.DefaultModel)} '{options.DefaultModel}' is not present in " +
                $"{nameof(options.AllowedModels)}.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Timeout)} must be a positive duration.");
        }

        return ValidateOptionsResult.Success;
    }
}
