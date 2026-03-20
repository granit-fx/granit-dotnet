using FluentValidation.TestHelper;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Options;
using Granit.AI.Endpoints.Validators;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Granit.AI.Endpoints.Tests.Validators;

public sealed class AIEmbeddingRequestValidatorTests
{
    private readonly AIEmbeddingRequestValidator _validator;

    public AIEmbeddingRequestValidatorTests()
    {
        IOptions<AIEndpointsOptions> options = Substitute.For<IOptions<AIEndpointsOptions>>();
        options.Value.Returns(new AIEndpointsOptions { MaxEmbeddingInputs = 3 });
        _validator = new AIEmbeddingRequestValidator(options);
    }

    [Fact]
    public void Valid_request_passes()
    {
        AIEmbeddingRequest request = new(["Hello world"]);
        TestValidationResult<AIEmbeddingRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_inputs_fails()
    {
        AIEmbeddingRequest request = new([]);
        TestValidationResult<AIEmbeddingRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Inputs);
    }

    [Fact]
    public void Too_many_inputs_fails()
    {
        AIEmbeddingRequest request = new(["a", "b", "c", "d"]);
        TestValidationResult<AIEmbeddingRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Inputs);
    }

    [Fact]
    public void Empty_input_text_fails()
    {
        AIEmbeddingRequest request = new([""]);
        TestValidationResult<AIEmbeddingRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Inputs[0]");
    }
}
