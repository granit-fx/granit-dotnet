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

    [Theory]
    [InlineData("garbage")]
    [InlineData("Bearer sk-ant-foo")]
    [InlineData("%vault:anthropic-api-key%")]
    public void Validate_ApiKeyMissingAnthropicPrefix_Fails(string apiKey)
    {
        AnthropicProviderOptions options = new() { ApiKey = apiKey };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("sk-ant-");
    }

    [Fact]
    public void Validate_BlankDefaultModel_Fails()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-key",
            DefaultModel = "   ",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultModel));
    }

    [Fact]
    public void Validate_DefaultModelNotInAllowedModels_Fails()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-key",
            DefaultModel = "claude-opus-4-7",
            AllowedModels = ["claude-sonnet-4-6"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AllowedModels");
    }

    [Fact]
    public void Validate_DefaultModelInsideAllowedModels_Succeeds()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-key",
            DefaultModel = "claude-sonnet-4-6",
            AllowedModels = ["claude-sonnet-4-6", "claude-haiku-4-5"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-key",
            Timeout = TimeSpan.Zero,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.Timeout));
    }

    [Fact]
    public void Validate_NegativeMaxRetries_Fails()
    {
        AnthropicProviderOptions options = new()
        {
            ApiKey = "sk-ant-key",
            MaxRetries = -1,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.MaxRetries));
    }
}
