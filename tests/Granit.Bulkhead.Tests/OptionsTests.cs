using Granit.Bulkhead.Internal;
using Granit.Bulkhead.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Bulkhead.Tests;

public sealed class OptionsTests
{
    // =========================================================================
    // Default values
    // =========================================================================

    [Fact]
    public void GranitBulkheadOptions_DefaultValues()
    {
        var options = new GranitBulkheadOptions();

        options.Enabled.ShouldBeTrue();
        options.BypassRoles.ShouldBeEmpty();
        options.UseFeatureBasedQuotas.ShouldBeFalse();
        options.Policies.ShouldBeEmpty();
        options.IdleTimeout.ShouldBe(TimeSpan.FromMinutes(30));
        options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void BulkheadPolicyOptions_DefaultValues()
    {
        var policy = new BulkheadPolicyOptions();

        policy.PermitLimit.ShouldBe(10);
        policy.QueueLimit.ShouldBe(0);
        policy.QueueTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        policy.FeatureName.ShouldBeNull();
    }

    [Fact]
    public void SectionName_IsBulkhead() =>
        GranitBulkheadOptions.SectionName.ShouldBe("Bulkhead");

    [Fact]
    public void Policies_CaseInsensitive()
    {
        var options = new GranitBulkheadOptions
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["Api"] = new() { PermitLimit = 20 },
            },
        };

        options.Policies.TryGetValue("api", out BulkheadPolicyOptions? policy).ShouldBeTrue();
        policy!.PermitLimit.ShouldBe(20);
    }

    // =========================================================================
    // Validator
    // =========================================================================

    [Fact]
    public void Validator_ValidOptions_Succeeds()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 20 },
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validator_ZeroIdleTimeout_Fails()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions { IdleTimeout = TimeSpan.Zero };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("IdleTimeout");
    }

    [Fact]
    public void Validator_ZeroCleanupInterval_Fails()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions { CleanupInterval = TimeSpan.Zero };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CleanupInterval");
    }

    [Fact]
    public void Validator_ZeroPermitLimit_Fails()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 0 },
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("PermitLimit");
    }

    [Fact]
    public void Validator_NegativeQueueLimit_Fails()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { QueueLimit = -1 },
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("QueueLimit");
    }

    [Fact]
    public void Validator_QueueLimitWithZeroTimeout_Fails()
    {
        var validator = new GranitBulkheadOptionsValidator();
        var options = new GranitBulkheadOptions
        {
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { QueueLimit = 5, QueueTimeout = TimeSpan.Zero },
            },
        };

        ValidateOptionsResult result = validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("QueueTimeout");
    }
}
