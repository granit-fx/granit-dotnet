using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Granit.AI.OpenAI.Options;

/// <summary>
/// Validates <see cref="OpenAIProviderOptions"/> at startup (fail-fast on missing configuration).
/// </summary>
internal sealed class OpenAIProviderOptionsValidator : IValidateOptions<OpenAIProviderOptions>
{
    /// <summary>
    /// Heuristic prefix shipped by OpenAI on every issued API key. Kept as a soft fail-fast —
    /// accepts unknown future formats by widening or removing this check.
    /// </summary>
    [SuppressMessage(
        "Security",
        "GRSEC003:Potential hardcoded secret detected",
        Justification = "Public format discriminator, not a secret. Used to reject placeholder bindings at startup.")]
    private const string ExpectedApiKeyPrefix = "sk-";

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, OpenAIProviderOptions options)
    {
        // ApiKey is no longer mandatory at the Host layer: tenants may configure their own via
        // Granit.Settings, or a workspace may carry its own credential. When set at the Host,
        // we keep the format heuristic to reject placeholders.
        if (!string.IsNullOrWhiteSpace(options.ApiKey) &&
            !options.ApiKey.StartsWith(ExpectedApiKeyPrefix, StringComparison.Ordinal))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.ApiKey)} does not look like an OpenAI API key " +
                $"(expected '{ExpectedApiKeyPrefix}' prefix). Verify the Vault binding " +
                "did not resolve to a placeholder or wrong secret.");
        }

        if (!string.IsNullOrEmpty(options.Endpoint) &&
            (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Endpoint)} must be a valid HTTP or HTTPS URI when set. " +
                $"Got: '{options.Endpoint}'.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultModel))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.DefaultModel)} must be non-empty.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultEmbeddingModel))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.DefaultEmbeddingModel)} must be non-empty.");
        }

        if (options.AllowedModels.Count > 0)
        {
            if (!options.AllowedModels.Contains(options.DefaultModel, StringComparer.Ordinal))
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.DefaultModel)} '{options.DefaultModel}' is not present in " +
                    $"{nameof(options.AllowedModels)}.");
            }

            if (!options.AllowedModels.Contains(options.DefaultEmbeddingModel, StringComparer.Ordinal))
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.DefaultEmbeddingModel)} '{options.DefaultEmbeddingModel}' is not present in " +
                    $"{nameof(options.AllowedModels)}.");
            }
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.Timeout)} must be a positive duration.");
        }

        if (options.MaxRetries < 0)
        {
            return ValidateOptionsResult.Fail($"{nameof(options.MaxRetries)} must be greater than or equal to 0.");
        }

        return ValidateOptionsResult.Success;
    }
}
