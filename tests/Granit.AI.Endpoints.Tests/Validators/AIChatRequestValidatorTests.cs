using FluentValidation.TestHelper;
using Granit.AI.Endpoints.Dtos;
using Granit.AI.Endpoints.Options;
using Granit.AI.Endpoints.Validators;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Granit.AI.Endpoints.Tests.Validators;

public sealed class AIChatRequestValidatorTests
{
    private readonly AIChatRequestValidator _validator;

    public AIChatRequestValidatorTests()
    {
        IOptions<AIEndpointsOptions> options = Substitute.For<IOptions<AIEndpointsOptions>>();
        options.Value.Returns(new AIEndpointsOptions { MaxChatMessages = 3 });
        _validator = new AIChatRequestValidator(options);
    }

    [Fact]
    public void Valid_request_passes()
    {
        AIChatRequest request = new([new("user", "Hello")]);
        TestValidationResult<AIChatRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Empty_messages_fails()
    {
        AIChatRequest request = new([]);
        TestValidationResult<AIChatRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Messages);
    }

    [Fact]
    public void Too_many_messages_fails()
    {
        List<AIChatMessageRequest> messages =
        [
            new("user", "1"),
            new("assistant", "2"),
            new("user", "3"),
            new("assistant", "4"),
        ];
        AIChatRequest request = new(messages);
        TestValidationResult<AIChatRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Messages);
    }

    [Fact]
    public void Invalid_role_fails()
    {
        AIChatRequest request = new([new("invalid-role", "Hello")]);
        TestValidationResult<AIChatRequest> result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor("Messages[0].Role");
    }

    [Theory]
    [InlineData("user")]
    [InlineData("assistant")]
    [InlineData("system")]
    public void Valid_roles_pass(string role)
    {
        AIChatRequest request = new([new(role, "Hello")]);
        TestValidationResult<AIChatRequest> result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
