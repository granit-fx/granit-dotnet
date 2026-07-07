using Granit.BackgroundJobs.Internal;
using Granit.BackgroundJobs.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests;

public sealed class BackgroundJobsOptionsValidatorTests
{
    private readonly BackgroundJobsOptionsValidator _sut = new();

    [Fact]
    public void Validate_Defaults_ReturnsSuccess()
    {
        // Arrange
        BackgroundJobsOptions options = new();

        // Act
        ValidateOptionsResult result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Validate_PositiveFailureAlertThreshold_ReturnsSuccess(int threshold)
    {
        // Arrange
        BackgroundJobsOptions options = new() { FailureAlertThreshold = threshold };

        // Act
        ValidateOptionsResult result = _sut.Validate(null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validate_NonPositiveFailureAlertThreshold_ReturnsFailed(int threshold)
    {
        // Arrange
        BackgroundJobsOptions options = new() { FailureAlertThreshold = threshold };

        // Act
        ValidateOptionsResult result = _sut.Validate(null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(BackgroundJobsOptions.FailureAlertThreshold));
    }
}
