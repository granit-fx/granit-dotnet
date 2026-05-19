using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Options;

/// <summary>
/// Validates <see cref="AzureOpenAIProviderOptions"/> at startup.
/// </summary>
internal sealed class AzureOpenAIProviderOptionsValidator : IValidateOptions<AzureOpenAIProviderOptions>
{
    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, AzureOpenAIProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Endpoint)} must be non-empty. " +
                "Set it to your Azure OpenAI resource endpoint (e.g. https://my-resource.openai.azure.com).");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(options.Endpoint)} must be a valid HTTPS URI. Got: '{options.Endpoint}'.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultDeployment))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.DefaultDeployment)} must be non-empty.");
        }

        if (string.IsNullOrWhiteSpace(options.DefaultEmbeddingDeployment))
        {
            return ValidateOptionsResult.Fail($"{nameof(options.DefaultEmbeddingDeployment)} must be non-empty.");
        }

        if (options.AllowedDeployments.Count > 0)
        {
            if (!options.AllowedDeployments.Contains(options.DefaultDeployment, StringComparer.Ordinal))
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.DefaultDeployment)} '{options.DefaultDeployment}' is not present in " +
                    $"{nameof(options.AllowedDeployments)}.");
            }

            if (!options.AllowedDeployments.Contains(options.DefaultEmbeddingDeployment, StringComparer.Ordinal))
            {
                return ValidateOptionsResult.Fail(
                    $"{nameof(options.DefaultEmbeddingDeployment)} '{options.DefaultEmbeddingDeployment}' is not present in " +
                    $"{nameof(options.AllowedDeployments)}.");
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
