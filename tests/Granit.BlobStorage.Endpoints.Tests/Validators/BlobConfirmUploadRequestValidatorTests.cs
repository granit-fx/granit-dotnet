using FluentValidation.TestHelper;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Validators;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Validators;

public sealed class BlobConfirmUploadRequestValidatorTests
{
    private readonly BlobConfirmUploadRequestValidator _validator = new();

    [Fact]
    public void Valid_request_should_pass()
    {
        BlobConfirmUploadRequest request = new("medical-images");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContainerName_empty_should_fail(string? containerName)
    {
        BlobConfirmUploadRequest request = new(containerName!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContainerName);
    }
}
