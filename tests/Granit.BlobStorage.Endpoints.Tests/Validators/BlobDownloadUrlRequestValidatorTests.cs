using FluentValidation.TestHelper;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Validators;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Validators;

public sealed class BlobDownloadUrlRequestValidatorTests
{
    private readonly BlobDownloadUrlRequestValidator _validator = new();

    [Fact]
    public void Valid_request_should_pass()
    {
        BlobDownloadUrlRequest request = new("medical-images", "download.pdf");
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContainerName_empty_should_fail(string? containerName)
    {
        BlobDownloadUrlRequest request = new(containerName!);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContainerName);
    }

    [Fact]
    public void FileName_null_should_pass()
    {
        BlobDownloadUrlRequest request = new("docs");
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.FileName);
    }

    [Fact]
    public void FileName_over_1024_chars_should_fail()
    {
        BlobDownloadUrlRequest request = new("docs", new string('x', 1025));
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.FileName);
    }
}
