using Granit.AI.OpenAI.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.OpenAI.Tests;

public sealed class OpenAIProviderOptionsValidatorTests
{
    private readonly OpenAIProviderOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidApiKey_Succeeds()
    {
        OpenAIProviderOptions options = new() { ApiKey = "sk-test-key-123" };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankApiKey_Succeeds(string apiKey)
    {
        // Empty Host ApiKey is now acceptable: tenant settings or workspace overrides may supply it.
        OpenAIProviderOptions options = new() { ApiKey = apiKey };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NullApiKey_Succeeds()
    {
        OpenAIProviderOptions options = new() { ApiKey = null! };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("Bearer sk-foo")]
    [InlineData("%vault:openai-api-key%")]
    public void Validate_ApiKeyMissingOpenAIPrefix_Fails(string apiKey)
    {
        OpenAIProviderOptions options = new() { ApiKey = apiKey };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("sk-");
    }

    [Fact]
    public void Validate_NonAbsoluteEndpoint_Fails()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            Endpoint = "not-a-url",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.Endpoint));
    }

    [Fact]
    public void Validate_NullEndpoint_IsAccepted()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            Endpoint = null,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_DefaultModelNotInAllowedModels_Fails()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            DefaultModel = "o3",
            DefaultEmbeddingModel = "text-embedding-3-small",
            AllowedModels = ["gpt-4o", "text-embedding-3-small"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultModel));
    }

    [Fact]
    public void Validate_DefaultEmbeddingModelNotInAllowedModels_Fails()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            DefaultModel = "gpt-4o",
            DefaultEmbeddingModel = "text-embedding-3-large",
            AllowedModels = ["gpt-4o"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultEmbeddingModel));
    }

    [Fact]
    public void Validate_BothDefaultsInsideAllowedModels_Succeeds()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            DefaultModel = "gpt-4o",
            DefaultEmbeddingModel = "text-embedding-3-small",
            AllowedModels = ["gpt-4o", "text-embedding-3-small"],
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            Timeout = TimeSpan.Zero,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.Timeout));
    }

    [Fact]
    public void Validate_NegativeMaxRetries_Fails()
    {
        OpenAIProviderOptions options = new()
        {
            ApiKey = "sk-key",
            MaxRetries = -1,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.MaxRetries));
    }
}
