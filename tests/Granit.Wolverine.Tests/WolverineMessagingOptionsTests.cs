// =============================================================================
// Tests - WolverineMessagingOptions + WolverineMessagingOptionsValidator
// =============================================================================
// Verifies default values, section name constant, and all validation branches:
// MaxRetryAttempts < 1, RetryDelays empty, any delay ≤ 0, and the happy path.
// =============================================================================

using Granit.Wolverine.Internal;
using Granit.Wolverine.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Wolverine.Tests;

public sealed class WolverineMessagingOptionsTests
{
    // -----------------------------------------------------------------------
    // WolverineMessagingOptions — defaults
    // -----------------------------------------------------------------------

    [Fact]
    public void SectionName_IsWolverine() =>
        WolverineMessagingOptions.SectionName.ShouldBe("Wolverine");

    [Fact]
    public void DefaultMaxRetryAttempts_IsThree() =>
        new WolverineMessagingOptions().MaxRetryAttempts.ShouldBe(3);

    [Fact]
    public void DefaultRetryDelays_AreFiveThirtyAndFiveMin()
    {
        TimeSpan[] delays = new WolverineMessagingOptions().RetryDelays;

        delays.ShouldBe(new[] { TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(5) });
    }

    // -----------------------------------------------------------------------
    // WolverineMessagingOptionsValidator — happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        WolverineMessagingOptionsValidator validator = new();

        ValidateOptionsResult result = validator.Validate(null, new WolverineMessagingOptions());

        result.Succeeded.ShouldBeTrue();
    }

    // -----------------------------------------------------------------------
    // WolverineMessagingOptionsValidator — MaxRetryAttempts
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_MaxRetryAttemptsLessThanOne_Fails(int value)
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new() { MaxRetryAttempts = value };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("MaxRetryAttempts"));
    }

    // -----------------------------------------------------------------------
    // WolverineMessagingOptionsValidator — RetryDelays
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_RetryDelaysEmpty_Fails()
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new() { RetryDelays = [] };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RetryDelays"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_RetryDelayNotPositive_Fails(int seconds)
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new()
        {
            RetryDelays = [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(seconds)],
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(x => x.Contains("RetryDelays"));
    }

    [Fact]
    public void Validate_CustomValidDelays_Succeeds()
    {
        WolverineMessagingOptionsValidator validator = new();
        WolverineMessagingOptions options = new()
        {
            MaxRetryAttempts = 2,
            RetryDelays = [TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1)],
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
