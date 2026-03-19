using FluentValidation.TestHelper;
using Granit.BlobStorage.Endpoints.Dtos;
using Granit.BlobStorage.Endpoints.Validators;
using Xunit;

namespace Granit.BlobStorage.Endpoints.Tests.Validators;

public sealed class BlobUploadInitiateRequestValidatorTests
{
    private readonly BlobUploadInitiateRequestValidator _validator = new();

    [Fact]
    public void Valid_request_should_pass()
    {
        BlobUploadInitiateRequest request = new("medical-images", "radio.jpg", "image/jpeg", 10_000_000);
        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void ContainerName_empty_should_fail(string? containerName)
    {
        BlobUploadInitiateRequest request = new(containerName!, "file.pdf", "application/pdf", 1000);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContainerName);
    }

    [Theory]
    [InlineData("Medical-Images")]
    [InlineData("medical_images")]
    [InlineData("-invalid")]
    public void ContainerName_invalid_format_should_fail(string containerName)
    {
        BlobUploadInitiateRequest request = new(containerName, "file.pdf", "application/pdf", 1000);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContainerName);
    }

    [Theory]
    [InlineData("medical-images")]
    [InlineData("docs")]
    [InlineData("a1-b2-c3")]
    public void ContainerName_valid_format_should_pass(string containerName)
    {
        BlobUploadInitiateRequest request = new(containerName, "file.pdf", "application/pdf", 1000);
        _validator.TestValidate(request).ShouldNotHaveValidationErrorFor(x => x.ContainerName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void FileName_empty_should_fail(string? fileName)
    {
        BlobUploadInitiateRequest request = new("docs", fileName!, "application/pdf", 1000);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.FileName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("invalid")]
    public void ContentType_invalid_should_fail(string? contentType)
    {
        BlobUploadInitiateRequest request = new("docs", "file.pdf", contentType!, 1000);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContentType);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void SizeBytes_zero_or_negative_should_fail(long size)
    {
        BlobUploadInitiateRequest request = new("docs", "file.pdf", "application/pdf", size);
        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.SizeBytes);
    }
}
