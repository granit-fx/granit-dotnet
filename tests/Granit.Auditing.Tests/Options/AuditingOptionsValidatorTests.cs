using Granit.Auditing.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Auditing.Tests.Options;

public sealed class AuditingOptionsValidatorTests
{
    private readonly AuditingOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        AuditingOptions options = new();

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroConfigurationChangeRetention_Fails(double days)
    {
        AuditingOptions options = new() { ConfigurationChangeRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.ConfigurationChangeRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroDataMutationRetention_Fails(double days)
    {
        AuditingOptions options = new() { DataMutationRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.DataMutationRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroDataAccessRetention_Fails(double days)
    {
        AuditingOptions options = new() { DataAccessRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.DataAccessRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroAccessDeniedRetention_Fails(double days)
    {
        AuditingOptions options = new() { AccessDeniedRetention = TimeSpan.FromDays(days) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.AccessDeniedRetention));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroCleanupInterval_Fails(double minutes)
    {
        AuditingOptions options = new() { CleanupInterval = TimeSpan.FromMinutes(minutes) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.CleanupInterval));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroCacheEntryTtl_Fails(double seconds)
    {
        AuditingOptions options = new() { CacheEntryTtl = TimeSpan.FromSeconds(seconds) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.CacheEntryTtl));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_NegativeOrZeroCacheEntityQueryTtl_Fails(double seconds)
    {
        AuditingOptions options = new() { CacheEntityQueryTtl = TimeSpan.FromSeconds(seconds) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.CacheEntityQueryTtl));
    }

    [Fact]
    public void Validate_MultipleInvalidValues_ReportsAllFailures()
    {
        AuditingOptions options = new()
        {
            ConfigurationChangeRetention = TimeSpan.Zero,
            DataMutationRetention = TimeSpan.FromDays(-1),
            CleanupInterval = TimeSpan.Zero,
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.ConfigurationChangeRetention));
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.DataMutationRetention));
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.CleanupInterval));
    }

    [Fact]
    public void Validate_MinimumRetentionOneDay_Succeeds()
    {
        AuditingOptions options = new()
        {
            ConfigurationChangeRetention = TimeSpan.FromDays(1),
            DataMutationRetention = TimeSpan.FromDays(1),
            DataAccessRetention = TimeSpan.FromDays(1),
            AccessDeniedRetention = TimeSpan.FromDays(1),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MinimumCleanupIntervalOneMinute_Succeeds()
    {
        AuditingOptions options = new() { CleanupInterval = TimeSpan.FromMinutes(1) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RetentionJustBelowMinimum_Fails()
    {
        AuditingOptions options = new()
        {
            DataMutationRetention = TimeSpan.FromDays(1) - TimeSpan.FromTicks(1),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }
}
