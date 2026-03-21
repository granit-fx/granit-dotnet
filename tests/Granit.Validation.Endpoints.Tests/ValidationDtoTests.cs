using Granit.Validation.Endpoints.Dtos;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ValidationDtoTests
{
    // =========================================================================
    // ValidationFieldValidateRequest
    // =========================================================================

    [Fact]
    public void ValidationFieldValidateRequest_Properties_AreSet()
    {
        ValidationFieldValidateRequest request = new("Granit:Validation:InvalidIban", "BE68539007547034");

        request.ErrorCode.ShouldBe("Granit:Validation:InvalidIban");
        request.Value.ShouldBe("BE68539007547034");
    }

    [Fact]
    public void ValidationFieldValidateRequest_NullValue_IsAllowed()
    {
        ValidationFieldValidateRequest request = new("Granit:Validation:InvalidIban", null);

        request.Value.ShouldBeNull();
    }

    // =========================================================================
    // ValidationFieldValidateResponse
    // =========================================================================

    [Fact]
    public void ValidationFieldValidateResponse_Properties_AreSet()
    {
        ValidationFieldValidateResponse response = new("Granit:Validation:InvalidIban", ValidationFieldStatus.Valid);

        response.ErrorCode.ShouldBe("Granit:Validation:InvalidIban");
        response.Status.ShouldBe(ValidationFieldStatus.Valid);
    }

    // =========================================================================
    // ValidationFieldValidateBatchRequest
    // =========================================================================

    [Fact]
    public void ValidationFieldValidateBatchRequest_Fields_AreSet()
    {
        List<ValidationFieldValidateRequest> fields =
        [
            new("Granit:Validation:InvalidIban", "BE68539007547034"),
            new("Granit:Validation:InvalidEmail", "test@example.com"),
        ];

        ValidationFieldValidateBatchRequest request = new(fields);

        request.Fields.Count.ShouldBe(2);
    }

    // =========================================================================
    // ValidationFieldValidateBatchResponse
    // =========================================================================

    [Fact]
    public void ValidationFieldValidateBatchResponse_Results_AreSet()
    {
        List<ValidationFieldValidateResponse> results =
        [
            new("Granit:Validation:InvalidIban", ValidationFieldStatus.Valid),
            new("Granit:Validation:InvalidEmail", ValidationFieldStatus.Invalid),
        ];

        ValidationFieldValidateBatchResponse response = new(results);

        response.Results.Count.ShouldBe(2);
        response.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        response.Results[1].Status.ShouldBe(ValidationFieldStatus.Invalid);
    }
}
