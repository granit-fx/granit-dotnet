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
        ValidationFieldValidateRequest request = new("Validation:Format:Iban", "BE68539007547034");

        request.ErrorCode.ShouldBe("Validation:Format:Iban");
        request.Value.ShouldBe("BE68539007547034");
    }

    [Fact]
    public void ValidationFieldValidateRequest_NullValue_IsAllowed()
    {
        ValidationFieldValidateRequest request = new("Validation:Format:Iban", null);

        request.Value.ShouldBeNull();
    }

    // =========================================================================
    // ValidationFieldValidateResponse
    // =========================================================================

    [Fact]
    public void ValidationFieldValidateResponse_Properties_AreSet()
    {
        ValidationFieldValidateResponse response = new("Validation:Format:Iban", ValidationFieldStatus.Valid);

        response.ErrorCode.ShouldBe("Validation:Format:Iban");
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
            new("Validation:Format:Iban", "BE68539007547034"),
            new("Validation:Format:Email", "test@example.com"),
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
            new("Validation:Format:Iban", ValidationFieldStatus.Valid),
            new("Validation:Format:Email", ValidationFieldStatus.Invalid),
        ];

        ValidationFieldValidateBatchResponse response = new(results);

        response.Results.Count.ShouldBe(2);
        response.Results[0].Status.ShouldBe(ValidationFieldStatus.Valid);
        response.Results[1].Status.ShouldBe(ValidationFieldStatus.Invalid);
    }
}
