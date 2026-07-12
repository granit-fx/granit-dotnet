using Granit.Auditing.Domain;
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
    [InlineData(AuditCategory.ConfigurationChange, 0)]
    [InlineData(AuditCategory.ConfigurationChange, -1)]
    [InlineData(AuditCategory.DataMutation, 0)]
    [InlineData(AuditCategory.DataMutation, -1)]
    [InlineData(AuditCategory.DataAccess, 0)]
    [InlineData(AuditCategory.DataAccess, -1)]
    [InlineData(AuditCategory.AccessDenied, 0)]
    [InlineData(AuditCategory.AccessDenied, -1)]
    [InlineData(AuditCategory.PrivilegedAccess, 0)]
    [InlineData(AuditCategory.PrivilegedAccess, -1)]
    public void Validate_NegativeOrZeroRetention_Fails(AuditCategory category, double days)
    {
        AuditingOptions options = new();
        options.Retention[category] = TimeSpan.FromDays(days);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(category.ToString());
    }

    [Theory]
    [InlineData(AuditCategory.ConfigurationChange)]
    [InlineData(AuditCategory.DataMutation)]
    [InlineData(AuditCategory.DataAccess)]
    [InlineData(AuditCategory.AccessDenied)]
    [InlineData(AuditCategory.PrivilegedAccess)]
    public void Validate_RetentionBelowMinimumRetentionFloor_Fails(AuditCategory category)
    {
        AuditingOptions options = new();
        options.Retention[category] = options.MinimumRetention - TimeSpan.FromDays(1);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(category.ToString());
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.MinimumRetention));
    }

    [Fact]
    public void Validate_LoweredMinimumRetention_AllowsShorterCategoryRetention()
    {
        AuditingOptions options = new()
        {
            MinimumRetention = TimeSpan.FromDays(30),
        };
        options.Retention[AuditCategory.DataAccess] = TimeSpan.FromDays(90);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.5)]
    public void Validate_MinimumRetentionBelowOneDay_Fails(double days)
    {
        AuditingOptions options = new()
        {
            MinimumRetention = TimeSpan.FromDays(days),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditingOptions.MinimumRetention));
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
        AuditingOptions options = new();
        options.Retention[AuditCategory.ConfigurationChange] = TimeSpan.Zero;
        options.Retention[AuditCategory.DataMutation] = TimeSpan.FromDays(-1);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(AuditCategory.ConfigurationChange));
        result.FailureMessage.ShouldContain(nameof(AuditCategory.DataMutation));
    }

    [Fact]
    public void Validate_AllRetentionsAtLoweredMinimum_Succeeds()
    {
        AuditingOptions options = new()
        {
            MinimumRetention = TimeSpan.FromDays(1),
        };
        foreach (AuditCategory category in Enum.GetValues<AuditCategory>())
        {
            options.Retention[category] = TimeSpan.FromDays(1);
        }

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RetentionJustBelowMinimum_Fails()
    {
        AuditingOptions options = new();
        options.Retention[AuditCategory.DataMutation] = options.MinimumRetention - TimeSpan.FromTicks(1);

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }
}
