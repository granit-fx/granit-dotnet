using System.Security.Claims;
using Granit.Validation.Endpoints.Dtos;
using Granit.Validation.Endpoints.Extensions;
using Granit.Validation.ServerValidation;
using Microsoft.AspNetCore.Http;
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
            new DelegatingServerValidator("Validation:InvalidIban", value => value == "BE68539007547034"));

        var request = new ValidationFieldValidateRequest("Validation:InvalidIban", "BE68539007547034");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry, CreateAuthenticatedContext());

        Ok<ValidationFieldValidateResponse> ok = result.Result.ShouldBeOfType<Ok<ValidationFieldValidateResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.ErrorCode.ShouldBe("Validation:InvalidIban");
        ok.Value.Status.ShouldBe(ValidationFieldStatus.Valid);
    }

    [Fact]
    public void HandleValidate_InvalidValue_ReturnsInvalidStatus()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => false));

        var request = new ValidationFieldValidateRequest("Validation:InvalidIban", "INVALID");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry, CreateAnonymousContext());

        Ok<ValidationFieldValidateResponse> ok = result.Result.ShouldBeOfType<Ok<ValidationFieldValidateResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Status.ShouldBe(ValidationFieldStatus.Invalid);
    }

    [Fact]
    public void HandleValidate_UnknownCode_Returns404()
    {
        ServerValidatorRegistry registry = CreateRegistry();

        var request = new ValidationFieldValidateRequest("Validation:Unknown", "value");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry, CreateAnonymousContext());

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(404);
    }

    [Fact]
    public void HandleValidate_SensitiveValidator_Unauthenticated_Returns404()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidUsSsn", _ => true, isSensitive: true));

        var request = new ValidationFieldValidateRequest("Validation:InvalidUsSsn", "123-45-6789");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry, CreateAnonymousContext());

        ProblemHttpResult problem = result.Result.ShouldBeOfType<ProblemHttpResult>();
        problem.StatusCode.ShouldBe(404);
    }

    [Fact]
    public void HandleValidate_SensitiveValidator_Authenticated_Validates()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidUsSsn", _ => true, isSensitive: true));

        var request = new ValidationFieldValidateRequest("Validation:InvalidUsSsn", "123-45-6789");
        Results<Ok<ValidationFieldValidateResponse>, ProblemHttpResult> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidate(request, registry, CreateAuthenticatedContext());

        Ok<ValidationFieldValidateResponse> ok = result.Result.ShouldBeOfType<Ok<ValidationFieldValidateResponse>>();
        ok.Value.ShouldNotBeNull();
        ok.Value.Status.ShouldBe(ValidationFieldStatus.Valid);
    }

    // =========================================================================
    // Batch validate
    // =========================================================================

    [Fact]
    public void HandleValidateBatch_MixedResults_ReturnsCorrectStatuses()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", value => value == "BE68539007547034"),
            new DelegatingServerValidator("Validation:InvalidEmail", _ => false));

        var request = new ValidationFieldValidateBatchRequest(
        [
            new("Validation:InvalidIban", "BE68539007547034"),
            new("Validation:InvalidEmail", "bad"),
            new("Validation:Unknown", "value"),
        ]);

        Ok<ValidationFieldValidateBatchResponse> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidateBatch(request, registry, CreateAuthenticatedContext());

        ValidationFieldValidateBatchResponse ok = result.Value.ShouldNotBeNull();
        ok.Results.Count.ShouldBe(3);
        ok.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        ok.Results[1].Status.ShouldBe(ValidationFieldStatus.Invalid);
        ok.Results[2].Status.ShouldBe(ValidationFieldStatus.ValidatorNotFound);
    }

    [Fact]
    public void HandleValidateBatch_SensitiveValidator_Unauthenticated_ReturnsNotFound()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Validation:InvalidUsSsn", _ => true, isSensitive: true));

        var request = new ValidationFieldValidateBatchRequest(
        [
            new("Validation:InvalidIban", "BE68539007547034"),
            new("Validation:InvalidUsSsn", "123-45-6789"),
        ]);

        Ok<ValidationFieldValidateBatchResponse> result =
            ValidationEndpointRouteBuilderExtensions.HandleValidateBatch(request, registry, CreateAnonymousContext());

        ValidationFieldValidateBatchResponse ok = result.Value.ShouldNotBeNull();
        ok.Results.Count.ShouldBe(2);
        ok.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        ok.Results[1].Status.ShouldBe(ValidationFieldStatus.ValidatorNotFound);
    }

    // =========================================================================
    // Discovery
    // =========================================================================

    [Fact]
    public void HandleGetValidators_ReturnsSortedErrorCodes()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidEmail", _ => true),
            new DelegatingServerValidator("Validation:InvalidBicSwift", _ => true));

        Ok<IReadOnlyList<string>> result =
            ValidationEndpointRouteBuilderExtensions.HandleGetValidators(registry, CreateAnonymousContext());

        IReadOnlyList<string> codes = result.Value.ShouldNotBeNull();
        codes.Count.ShouldBe(2);
        codes[0].ShouldBe("Validation:InvalidBicSwift");
        codes[1].ShouldBe("Validation:InvalidEmail");
    }

    [Fact]
    public void HandleGetValidators_HidesSensitiveFromAnonymous()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Validation:InvalidUsSsn", _ => true, isSensitive: true));

        Ok<IReadOnlyList<string>> result =
            ValidationEndpointRouteBuilderExtensions.HandleGetValidators(registry, CreateAnonymousContext());

        IReadOnlyList<string> codes = result.Value.ShouldNotBeNull();
        codes.Count.ShouldBe(1);
        codes[0].ShouldBe("Validation:InvalidIban");
    }

    [Fact]
    public void HandleGetValidators_ShowsSensitiveToAuthenticated()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Validation:InvalidUsSsn", _ => true, isSensitive: true));

        Ok<IReadOnlyList<string>> result =
            ValidationEndpointRouteBuilderExtensions.HandleGetValidators(registry, CreateAuthenticatedContext());

        IReadOnlyList<string> codes = result.Value.ShouldNotBeNull();
        codes.Count.ShouldBe(2);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static DefaultHttpContext CreateAnonymousContext()
    {
        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }

    private static DefaultHttpContext CreateAuthenticatedContext()
    {
        DefaultHttpContext context = new();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "testuser")], "TestAuth"));
        return context;
    }

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
