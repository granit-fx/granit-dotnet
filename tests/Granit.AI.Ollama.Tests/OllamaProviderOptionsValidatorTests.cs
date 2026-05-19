using Granit.AI.Ollama.Options;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Granit.AI.Ollama.Tests;

public sealed class OllamaProviderOptionsValidatorTests
{
    private static readonly OllamaProviderOptionsValidator Validator = new();

    private static OllamaProviderOptions ValidOptions() => new()
    {
        Endpoint = "http://localhost:11434",
        DefaultModel = "llama3.2",
    };

    [Fact]
    public void Validate_ValidEndpoint_Succeeds()
    {
        ValidateOptionsResult result = Validator.Validate(null, ValidOptions());

        result.Failed.ShouldBeFalse();
    }

    [Fact]
    public void Validate_HttpsEndpoint_Succeeds()
    {
        OllamaProviderOptions options = ValidOptions();
        options.Endpoint = "https://ollama.internal:11434";

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://localhost:11434")]
    [InlineData("tcp://localhost:11434")]
    public void Validate_InvalidEndpoint_Fails(string endpoint)
    {
        OllamaProviderOptions options = ValidOptions();
        options.Endpoint = endpoint;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(OllamaProviderOptions.Endpoint));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyEndpoint_Fails(string endpoint)
    {
        OllamaProviderOptions options = ValidOptions();
        options.Endpoint = endpoint;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(OllamaProviderOptions.Endpoint));
    }

    [Fact]
    public void Validate_BlankDefaultModel_Fails()
    {
        OllamaProviderOptions options = ValidOptions();
        options.DefaultModel = "   ";

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(OllamaProviderOptions.DefaultModel));
    }

    [Fact]
    public void Validate_DefaultModelNotInAllowedModels_Fails()
    {
        OllamaProviderOptions options = ValidOptions();
        options.DefaultModel = "phi3";
        options.AllowedModels = ["llama3.2"];

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(OllamaProviderOptions.AllowedModels));
    }

    [Fact]
    public void Validate_DefaultModelInsideAllowedModels_Succeeds()
    {
        OllamaProviderOptions options = ValidOptions();
        options.AllowedModels = ["llama3.2", "phi3"];

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NonPositiveTimeout_Fails()
    {
        OllamaProviderOptions options = ValidOptions();
        options.Timeout = TimeSpan.Zero;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(OllamaProviderOptions.Timeout));
    }
}
