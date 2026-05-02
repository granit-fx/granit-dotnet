using FluentValidation.Results;
using Granit.Documents.Endpoints.Documents.Dtos;
using Granit.Documents.Endpoints.Documents.Validators;
using Shouldly;
using Xunit;

namespace Granit.Documents.Endpoints.Tests.Documents.Validators;

public sealed class UploadTicketRequestValidatorTests
{
    private readonly UploadTicketRequestValidator _validator = new();

    [Fact]
    public async Task ValidRequest_PassesValidation()
    {
        var request = new UploadTicketRequest("contract.pdf", "application/pdf", 1024 * 1024);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyFileName_Fails(string fileName)
    {
        var request = new UploadTicketRequest(fileName, "application/pdf", 1024);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task EmptyContentType_Fails(string contentType)
    {
        var request = new UploadTicketRequest("a.pdf", contentType, 1024);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task NonPositiveMaxAllowedBytes_Fails(long maxBytes)
    {
        var request = new UploadTicketRequest("a.pdf", "application/pdf", maxBytes);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task MaxAllowedBytes_AboveCeiling_Fails()
    {
        var request = new UploadTicketRequest(
            "a.pdf",
            "application/pdf",
            UploadTicketRequestValidator.MaxAllowedBytesCeiling + 1);

        ValidationResult result = await _validator.ValidateAsync(request, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}
