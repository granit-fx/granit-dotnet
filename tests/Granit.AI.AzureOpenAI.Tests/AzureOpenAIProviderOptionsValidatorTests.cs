using Granit.AI.AzureOpenAI.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.AzureOpenAI.Tests;

public sealed class AzureOpenAIProviderOptionsValidatorTests
{
    private readonly AzureOpenAIProviderOptionsValidator _validator = new();

    private static AzureOpenAIProviderOptions ValidOptions() => new()
    {
        Endpoint = "https://my-resource.openai.azure.com",
        ApiKey = "test-key",
        DefaultDeployment = "gpt-4o",
        DefaultEmbeddingDeployment = "text-embedding-3-small",
    };

    [Fact]
    public void Validate_ValidEndpoint_Succeeds() =>
        _validator.Validate(null, ValidOptions()).Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_EmptyEndpoint_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.Endpoint = "";

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_HttpEndpoint_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.Endpoint = "http://my-resource.openai.azure.com";

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_BlankDefaultDeployment_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.DefaultDeployment = "   ";

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultDeployment));
    }

    [Fact]
    public void Validate_BlankDefaultEmbeddingDeployment_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.DefaultEmbeddingDeployment = "";

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultEmbeddingDeployment));
    }

    [Fact]
    public void Validate_DefaultDeploymentNotInAllowedDeployments_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.AllowedDeployments = ["o3-mini", "text-embedding-3-small"];

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultDeployment));
    }

    [Fact]
    public void Validate_DefaultEmbeddingDeploymentNotInAllowedDeployments_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.AllowedDeployments = ["gpt-4o"];

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.DefaultEmbeddingDeployment));
    }

    [Fact]
    public void Validate_BothDefaultsInsideAllowedDeployments_Succeeds()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.AllowedDeployments = ["gpt-4o", "text-embedding-3-small"];

        _validator.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.Timeout = TimeSpan.Zero;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.Timeout));
    }

    [Fact]
    public void Validate_NegativeMaxRetries_Fails()
    {
        AzureOpenAIProviderOptions options = ValidOptions();
        options.MaxRetries = -1;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(options.MaxRetries));
    }
}
