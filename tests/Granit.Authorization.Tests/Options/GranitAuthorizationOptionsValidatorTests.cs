// =============================================================================
// GranitAuthorizationOptionsValidatorTests - Options validation rules
// =============================================================================
// Verifies:
//   - Valid options pass validation
//   - Empty or null AdminRoles fail validation
//   - CacheDuration below 10 seconds fails
//   - CacheDuration above 30 minutes fails
//   - Boundary values (10s, 30m) are accepted
// =============================================================================

using Granit.Authorization.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests.Options;

public sealed class GranitAuthorizationOptionsValidatorTests
{
    private readonly GranitAuthorizationOptionsValidator _validator = new(
        CreateHostEnvironment(Environments.Development));

    // =========================================================================
    // Valid options
    // =========================================================================

    [Fact]
    public void Validate_ValidDefaults_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new();

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CustomValidOptions_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = ["superadmin", "root"],
            CacheDuration = TimeSpan.FromMinutes(10),
            AlwaysAllow = false
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithNamedOptions_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new();

        ValidateOptionsResult result = _validator.Validate("SomeName", options);

        result.Succeeded.ShouldBeTrue();
    }

    // =========================================================================
    // AdminRoles validation
    // =========================================================================

    [Fact]
    public void Validate_EmptyAdminRoles_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = []
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AdminRoles");
    }

    [Fact]
    public void Validate_NullAdminRoles_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = null!
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AdminRoles");
    }

    [Fact]
    public void Validate_SingleAdminRole_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = ["admin"]
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_MultipleAdminRoles_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = ["admin", "superadmin", "root"]
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // =========================================================================
    // CacheDuration validation
    // =========================================================================

    [Fact]
    public void Validate_CacheDurationTooShort_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromSeconds(5)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CacheDuration");
    }

    [Fact]
    public void Validate_CacheDurationTooLong_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromMinutes(45)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CacheDuration");
    }

    [Fact]
    public void Validate_CacheDurationZero_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.Zero
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CacheDurationNegative_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromSeconds(-1)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CacheDurationOneHour_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromHours(1)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    // =========================================================================
    // Boundary values
    // =========================================================================

    [Fact]
    public void Validate_CacheDurationExactly10Seconds_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromSeconds(10)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CacheDurationExactly30Minutes_ReturnsSuccess()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromMinutes(30)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CacheDurationJustBelow10Seconds_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromSeconds(9)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CacheDurationJustAbove30Minutes_ReturnsFail()
    {
        GranitAuthorizationOptions options = new()
        {
            CacheDuration = TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1))
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    // =========================================================================
    // Combined invalid scenarios
    // =========================================================================

    [Fact]
    public void Validate_EmptyAdminRolesAndValidCache_FailsOnAdminRoles()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = [],
            CacheDuration = TimeSpan.FromMinutes(5)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AdminRoles");
    }

    [Fact]
    public void Validate_ValidAdminRolesAndInvalidCache_FailsOnCacheDuration()
    {
        GranitAuthorizationOptions options = new()
        {
            AdminRoles = ["admin"],
            CacheDuration = TimeSpan.FromSeconds(1)
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CacheDuration");
    }

    // =========================================================================
    // AlwaysAllow production guard
    // =========================================================================

    [Fact]
    public void Validate_AlwaysAllowInDevelopment_ReturnsSuccess()
    {
        GranitAuthorizationOptionsValidator devValidator = new(
            CreateHostEnvironment(Environments.Development));
        GranitAuthorizationOptions options = new() { AlwaysAllow = true };

        ValidateOptionsResult result = devValidator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_AlwaysAllowInProduction_ReturnsFail()
    {
        GranitAuthorizationOptionsValidator prodValidator = new(
            CreateHostEnvironment(Environments.Production));
        GranitAuthorizationOptions options = new() { AlwaysAllow = true };

        ValidateOptionsResult result = prodValidator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AlwaysAllow");
        result.FailureMessage.ShouldContain("production");
    }

    [Fact]
    public void Validate_AlwaysAllowFalseInProduction_ReturnsSuccess()
    {
        GranitAuthorizationOptionsValidator prodValidator = new(
            CreateHostEnvironment(Environments.Production));
        GranitAuthorizationOptions options = new() { AlwaysAllow = false };

        ValidateOptionsResult result = prodValidator.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    // =========================================================================
    // Interface conformance
    // =========================================================================

    [Fact]
    public void ImplementsIValidateOptions() =>
        _validator.ShouldBeAssignableTo<IValidateOptions<GranitAuthorizationOptions>>();

    private static IHostEnvironment CreateHostEnvironment(string environmentName)
    {
        IHostEnvironment env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);
        return env;
    }
}
