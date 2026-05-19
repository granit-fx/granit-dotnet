using Granit.AI.Anthropic.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.Anthropic.Tests;

public sealed class AnthropicProviderOptionsValidatorTests
{
    private readonly AnthropicProviderOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidApiKey_Succeeds()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-valid-key",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyApiKey_Fails(string? apiKey)
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = apiKey!,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ApiKey");
    }
}
