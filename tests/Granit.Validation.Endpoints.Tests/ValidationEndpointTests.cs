using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Extensions;
using Granit.Validation.ServerValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationEndpointTests
{
    // =========================================================================
    // Single validate
    // =========================================================================

    [Fact]
    public void HandleValidate_ValidValue_ReturnsValidStatus()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", value => value == "BE68539007547034"));

        var request = new ValidationFieldValidateRequest("Granit:Validation:InvalidIban", "BE68539007547034");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result = ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry);

        Ok<ValidationFieldValidateResponse> ok = result.Result.ShouldBeOfType<Ok<ValidationFieldValidateResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.ErrorCode.ShouldBe("Granit:Validation:InvalidIban");
        ok.Value.Status.ShouldBe(ValidationFieldStatus.Valid);
    }

    [Fact]
    public void HandleValidate_InvalidValue_ReturnsInvalidStatus()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => false));

        var request = new ValidationFieldValidateRequest("Granit:Validation:InvalidIban", "INVALID");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result = ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry);

        Ok<ValidationFieldValidateResponse> ok = result.Result.ShouldBeOfType<Ok<ValidationFieldValidateResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Status.ShouldBe(ValidationFieldStatus.Invalid);
    }

    [Fact]
    public void HandleValidate_UnknownCode_Returns404()
    {
        ServerValidatorRegistry registry = CreateRegistry();

        var request = new ValidationFieldValidateRequest("Granit:Validation:Unknown", "value");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result = ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry);

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(404);
    }

    // =========================================================================
    // Batch validate
    // =========================================================================

    [Fact]
    public void HandleValidateBatch_MixedResults_ReturnsCorrectStatuses()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", value => value == "BE68539007547034"),
            new DelegatingServerValidator("Granit:Validation:InvalidEmail", _ => false));

        var request = new ValidationFieldValidateBatchRequest(
        [
            new("Granit:Validation:InvalidIban", "BE68539007547034"),
            new("Granit:Validation:InvalidEmail", "bad"),
            new("Granit:Validation:Unknown", "value"),
        ]);

        Ok<ValidationFieldValidateBatchResponse> result = ValidationEndpointRouteBuilderExtensions.HandleValidateBatch(request, registry);

        ValidationFieldValidateBatchResponse ok = result.Value.ShouldNotBeNull();
        ok.Results.Count.ShouldBe(3);
        ok.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        ok.Results[1].Status.ShouldBe(ValidationFieldStatus.Invalid);
        ok.Results[2].Status.ShouldBe(ValidationFieldStatus.ValidatorNotFound);
    }

    // =========================================================================
    // Discovery
    // =========================================================================

    [Fact]
    public void HandleGetValidators_ReturnsSortedErrorCodes()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidEmail", _ => true),
            new DelegatingServerValidator("Granit:Validation:InvalidBicSwift", _ => true));

        Ok<IReadOnlyList<string>> result = ValidationEndpointRouteBuilderExtensions.HandleGetValidators(registry);

        IReadOnlyList<string> codes = result.Value.ShouldNotBeNull();
        codes.Count.ShouldBe(2);
        codes[0].ShouldBe("Granit:Validation:InvalidBicSwift");
        codes[1].ShouldBe("Granit:Validation:InvalidEmail");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static ServerValidatorRegistry CreateRegistry(params IServerValidator[] validators)
    {
        var contributor = new TestContributor(validators);
        ILogger<ServerValidatorRegistry> logger = Substitute.For<ILogger<ServerValidatorRegistry>>();
        return new ServerValidatorRegistry([contributor], logger);
    }

    private sealed class TestContributor(IServerValidator[] validators) : IServerValidatorContributor
    {
        public IEnumerable<IServerValidator> GetValidators() => validators;
    }
}
