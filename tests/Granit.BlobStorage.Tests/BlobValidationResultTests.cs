using Shouldly;
using Xunit;

namespace Granit.BlobStorage.Tests;

public sealed class BlobValidationResultTests
{
    [Fact]
    public void Success_WithoutVerifiedContentType_ReturnsValidResult()
    {
        var result = BlobValidationResult.Success();

        result.IsValid.ShouldBeTrue();
        result.FailureReason.ShouldBeNull();
        result.VerifiedContentType.ShouldBeNull();
    }

    [Fact]
    public void Success_WithVerifiedContentType_ReturnsValidResultWithContentType()
    {
        var result = BlobValidationResult.Success("application/pdf");

        result.IsValid.ShouldBeTrue();
        result.FailureReason.ShouldBeNull();
        result.VerifiedContentType.ShouldBe("application/pdf");
    }

    [Fact]
    public void Failure_ReturnsInvalidResultWithReason()
    {
        var result = BlobValidationResult.Failure("File too large");

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldBe("File too large");
        result.VerifiedContentType.ShouldBeNull();
    }
}
