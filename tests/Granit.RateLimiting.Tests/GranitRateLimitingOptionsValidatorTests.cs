using Granit.RateLimiting.Internal;
using Granit.RateLimiting.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.RateLimiting.Tests;

public sealed class GranitRateLimitingOptionsValidatorTests
{
    private readonly GranitRateLimitingOptionsValidator _validator = new();

    [Fact]
    public void Validate_ValidOptions_ReturnsSuccess()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyKeyPrefix_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("KeyPrefix");
    }

    [Fact]
    public void Validate_WhitespaceKeyPrefix_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "   ",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("KeyPrefix");
    }

    [Fact]
    public void Validate_PermitLimitZero_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 0, Window = TimeSpan.FromMinutes(1) },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("PermitLimit");
    }

    [Fact]
    public void Validate_NegativePermitLimit_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = -1, Window = TimeSpan.FromMinutes(1) },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("PermitLimit");
    }

    [Fact]
    public void Validate_ZeroWindow_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 100, Window = TimeSpan.Zero },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Window");
    }

    [Fact]
    public void Validate_NegativeWindow_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 100, Window = TimeSpan.FromSeconds(-1) },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Window");
    }

    [Fact]
    public void Validate_TokenBucket_ZeroTokenLimit_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    TokenLimit = 0,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TokenLimit");
    }

    [Fact]
    public void Validate_TokenBucket_ZeroTokensPerPeriod_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    TokenLimit = 50,
                    TokensPerPeriod = 0,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("TokensPerPeriod");
    }

    [Fact]
    public void Validate_TokenBucket_ZeroReplenishmentPeriod_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    TokenLimit = 50,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.Zero,
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ReplenishmentPeriod");
    }

    [Fact]
    public void Validate_TokenBucket_ValidOptions_Succeeds()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.TokenBucket,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    TokenLimit = 50,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_SlidingWindow_ZeroSegments_Fails()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.SlidingWindow,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 0,
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("SegmentsPerWindow");
    }

    [Fact]
    public void Validate_SlidingWindow_ValidSegments_Succeeds()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.SlidingWindow,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NoPolicies_Succeeds()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MultiplePoliciesWithErrors_ReportsAll()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new() { PermitLimit = 0, Window = TimeSpan.FromMinutes(1) },
                ["webhook"] = new() { PermitLimit = 100, Window = TimeSpan.Zero },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("api");
        result.FailureMessage.ShouldContain("webhook");
    }

    [Fact]
    public void Validate_FixedWindow_DoesNotValidateTokenBucketParams()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.FixedWindow,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    TokenLimit = 0, // should be ignored for FixedWindow
                    TokensPerPeriod = 0, // should be ignored for FixedWindow
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_FixedWindow_DoesNotValidateSegmentsPerWindow()
    {
        GranitRateLimitingOptions options = new()
        {
            KeyPrefix = "rl",
            Policies = new Dictionary<string, RateLimitPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["api"] = new()
                {
                    Algorithm = RateLimitAlgorithm.FixedWindow,
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 0, // should be ignored for FixedWindow
                },
            },
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }
}
