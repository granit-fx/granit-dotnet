using FluentValidation.TestHelper;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Validators;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Validators;

public sealed class BlobDeleteRequestValidatorTests
{
    private readonly BlobDeleteRequestValidator _validator = new();

    [Fact]
    public void Valid_request_should_pass()
    {
        BlobDeleteRequest request = new("medical-images", "GDPR Art. 17");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContainerName_empty_should_fail(string? containerName)
    {
        BlobDeleteRequest request = new(containerName!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContainerName);
    }

    [Fact]
    public void DeletionReason_over_500_chars_should_fail()
    {
        BlobDeleteRequest request = new("docs", new string('x', 501));
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.DeletionReason);
    }

    [Fact]
    public void DeletionReason_null_should_pass()
    {
        BlobDeleteRequest request = new("docs");
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.DeletionReason);
    }
}
