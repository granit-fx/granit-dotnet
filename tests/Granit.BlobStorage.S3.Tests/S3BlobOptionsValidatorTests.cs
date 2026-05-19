using Granit.BlobStorage.S3.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.S3.Tests;

public sealed class S3BlobOptionsValidatorTests
{
    private static readonly S3BlobOptionsValidator Validator = new();

    private static S3BlobOptions ValidOptions() => new()
    {
        ServiceUrl = "https://s3.rbx.io.cloud.ovh.net",
        AccessKey = "AKIAIOSFODNN7EXAMPLE",
        SecretKey = "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
        DefaultBucket = "granit-blobs",
    };

    [Fact]
    public void Validate_AllRequiredFieldsPresent_ReturnsSuccess()
    {
        ValidateOptionsResult result = Validator.Validate(null, ValidOptions());

        result.Failed.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingServiceUrl_ReturnsFail(string serviceUrl)
    {
        S3BlobOptions options = ValidOptions();
        options.ServiceUrl = serviceUrl;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(S3BlobOptions.ServiceUrl));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingAccessKey_ReturnsFail(string accessKey)
    {
        S3BlobOptions options = ValidOptions();
        options.AccessKey = accessKey;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(S3BlobOptions.AccessKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingSecretKey_ReturnsFail(string secretKey)
    {
        S3BlobOptions options = ValidOptions();
        options.SecretKey = secretKey;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(S3BlobOptions.SecretKey));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingDefaultBucket_ReturnsFail(string bucket)
    {
        S3BlobOptions options = ValidOptions();
        options.DefaultBucket = bucket;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(S3BlobOptions.DefaultBucket));
    }

    [Fact]
    public void Validate_OvhcloudUrl_ReturnsSuccess()
    {
        S3BlobOptions options = ValidOptions();
        options.ServiceUrl = "https://s3.rbx.io.cloud.ovh.net";
        options.Region = "rbx";
        options.ForcePathStyle = false;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeFalse();
    }

    [Fact]
    public void Validate_MinioUrl_ReturnsSuccess()
    {
        S3BlobOptions options = ValidOptions();
        options.ServiceUrl = "http://localhost:9000";
        options.ForcePathStyle = true;

        ValidateOptionsResult result = Validator.Validate(null, options);

        result.Failed.ShouldBeFalse();
    }
}
