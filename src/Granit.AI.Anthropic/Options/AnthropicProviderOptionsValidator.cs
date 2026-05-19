using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Options;

/// <summary>Validates <see cref="AnthropicProviderOptions"/>.</summary>
internal sealed class AnthropicProviderOptionsValidator : IValidateOptions<AnthropicProviderOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AnthropicProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ValidateOptionsResult.Fail("AnthropicProviderOptions.ApiKey is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
