using Granit.BlobStorage.Database.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Options;

public sealed class DbStoreBlobOptionsValidatorTests
{
    private readonly DbStoreBlobOptionsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5 * 1024 * 1024)]
    public void Validate_MaxBlobSizeBytesZeroOrNegative_ReturnsFail(long value)
    {
        DbStoreBlobOptions options = new() { MaxBlobSizeBytes = value };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DbStoreBlobOptions.MaxBlobSizeBytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(512 * 1024)]
    [InlineData(50 * 1024 * 1024)]
    public void Validate_MaxBlobSizeBytesPositive_ReturnsSuccess(long value)
    {
        DbStoreBlobOptions options = new() { MaxBlobSizeBytes = value };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
