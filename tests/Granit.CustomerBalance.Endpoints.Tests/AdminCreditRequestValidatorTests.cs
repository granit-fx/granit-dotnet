using FluentValidation.TestHelper;
using Granit.CustomerBalance.Endpoints.Dtos;
using Granit.CustomerBalance.Endpoints.Validators;
using Xunit;

namespace Granit.CustomerBalance.Endpoints.Tests;

public sealed class AdminCreditRequestValidatorTests
{
    private readonly AdminCreditRequestValidator _validator = new();

    [Fact]
    public void Valid_Request_ShouldPass()
    {
        var request = new AdminCreditRequest(100m, "EUR", "Promotional", "Welcome credit", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Amount_Zero_ShouldFail()
    {
        var request = new AdminCreditRequest(0m, "EUR", "Promotional", "Test", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Amount_Negative_ShouldFail()
    {
        var request = new AdminCreditRequest(-10m, "EUR", "Promotional", "Test", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Currency_Empty_ShouldFail()
    {
        var request = new AdminCreditRequest(10m, "", "Promotional", "Test", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Currency_TooLong_ShouldFail()
    {
        var request = new AdminCreditRequest(10m, "EURO", "Promotional", "Test", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Currency);
    }

    [Fact]
    public void Source_Empty_ShouldFail()
    {
        var request = new AdminCreditRequest(10m, "EUR", "", "Test", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Source);
    }

    [Fact]
    public void Reason_Empty_ShouldFail()
    {
        var request = new AdminCreditRequest(10m, "EUR", "Promotional", "", null);

        TestValidationResult<AdminCreditRequest> result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Reason);
    }
}
