using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class IdempotencyOptionsValidatorTests
{
    private readonly IdempotencyOptionsValidator _validator = new();

    private static IdempotencyOptions ValidOptions() => new()
    {
        HeaderName = "Idempotency-Key",
        KeyPrefix = "idp",
        MaxBodySizeBytes = 1024 * 1024,
        ExecutionTimeout = TimeSpan.FromSeconds(25),
        InProgressTtl = TimeSpan.FromSeconds(30),
    };

    // -------------------------------------------------------------------------
    // Valid options
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        ValidateOptionsResult result = _validator.Validate(null, ValidOptions());

        result.Succeeded.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // HeaderName
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyHeaderName_Fails(string? headerName)
    {
        IdempotencyOptions options = ValidOptions();
        options.HeaderName = headerName!;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("HeaderName"));
    }

    // -------------------------------------------------------------------------
    // KeyPrefix
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyKeyPrefix_Fails(string? keyPrefix)
    {
        IdempotencyOptions options = ValidOptions();
        options.KeyPrefix = keyPrefix!;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("KeyPrefix"));
    }

    // -------------------------------------------------------------------------
    // MaxBodySizeBytes
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_MaxBodySizeBytes_ZeroOrNegative_Fails(int value)
    {
        IdempotencyOptions options = ValidOptions();
        options.MaxBodySizeBytes = value;

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f => f.Contains("MaxBodySizeBytes"));
    }

    // -------------------------------------------------------------------------
    // ExecutionTimeout vs InProgressTtl
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_ExecutionTimeout_EqualTo_InProgressTtl_Fails()
    {
        IdempotencyOptions options = ValidOptions();
        options.ExecutionTimeout = TimeSpan.FromSeconds(30);
        options.InProgressTtl = TimeSpan.FromSeconds(30);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.ShouldContain(f =>
            f.Contains("ExecutionTimeout") && f.Contains("InProgressTtl"));
    }

    [Fact]
    public void Validate_ExecutionTimeout_GreaterThan_InProgressTtl_Fails()
    {
        IdempotencyOptions options = ValidOptions();
        options.ExecutionTimeout = TimeSpan.FromSeconds(60);
        options.InProgressTtl = TimeSpan.FromSeconds(30);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ExecutionTimeout_LessThan_InProgressTtl_Succeeds()
    {
        IdempotencyOptions options = ValidOptions();
        options.ExecutionTimeout = TimeSpan.FromSeconds(10);
        options.InProgressTtl = TimeSpan.FromSeconds(30);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // Multiple failures
    // -------------------------------------------------------------------------

    [Fact]
    public void Validate_MultipleInvalidFields_ReportsAllFailures()
    {
        IdempotencyOptions options = new()
        {
            HeaderName = "",
            KeyPrefix = "",
            MaxBodySizeBytes = 0,
            ExecutionTimeout = TimeSpan.FromSeconds(30),
            InProgressTtl = TimeSpan.FromSeconds(30),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBeGreaterThanOrEqualTo(4);
    }
}
