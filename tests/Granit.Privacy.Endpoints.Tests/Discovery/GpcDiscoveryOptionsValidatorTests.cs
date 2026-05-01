using Granit.Privacy.Endpoints.Discovery;
using Granit.Privacy.Endpoints.Internal;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Endpoints.Tests.Discovery;

public sealed class GpcDiscoveryOptionsValidatorTests
{
    private readonly GpcDiscoveryOptionsValidator _validator = new();

    [Fact]
    public void Disabled_WithoutLastUpdate_IsValid()
    {
        GpcDiscoveryOptions options = new() { Enabled = false };

        ValidateOptionsResult result = _validator.Validate(name: null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Enabled_WithLastUpdate_IsValid()
    {
        GpcDiscoveryOptions options = new()
        {
            Enabled = true,
            LastUpdate = new DateOnly(2026, 5, 1),
        };

        ValidateOptionsResult result = _validator.Validate(name: null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Enabled_WithoutLastUpdate_FailsWithLegalAnchorMessage()
    {
        GpcDiscoveryOptions options = new() { Enabled = true, LastUpdate = null };

        ValidateOptionsResult result = _validator.Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("LastUpdate");
        result.FailureMessage.ShouldContain("legal anchor");
    }

    [Fact]
    public void Enabled_WithNegativeCacheMaxAge_Fails()
    {
        GpcDiscoveryOptions options = new()
        {
            Enabled = true,
            LastUpdate = new DateOnly(2026, 5, 1),
            CacheMaxAgeSeconds = -1,
        };

        ValidateOptionsResult result = _validator.Validate(name: null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("CacheMaxAgeSeconds");
    }

    [Fact]
    public void Validate_NullOptions_Throws() =>
        Should.Throw<ArgumentNullException>(() => _validator.Validate(name: null, options: null!));
}
