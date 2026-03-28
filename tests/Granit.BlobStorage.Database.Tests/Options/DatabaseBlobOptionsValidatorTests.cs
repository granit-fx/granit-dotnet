using Granit.BlobStorage.Database.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Database.Tests.Options;

public sealed class DatabaseBlobOptionsValidatorTests
{
    private readonly DatabaseBlobOptionsValidator _validator = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1000)]
    public void Validate_MaxBlobSizeBytesZeroOrNegative_ReturnsFail(long value)
    {
        DatabaseBlobOptions options = new() { MaxBlobSizeBytes = value };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(DatabaseBlobOptions.MaxBlobSizeBytes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(10 * 1024 * 1024)]
    public void Validate_MaxBlobSizeBytesPositive_ReturnsSuccess(long value)
    {
        DatabaseBlobOptions options = new() { MaxBlobSizeBytes = value };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
