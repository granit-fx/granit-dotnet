using Granit.OpenIddict.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class GranitKeyRotationOptionsValidatorTests
{
    private readonly GranitKeyRotationOptionsValidator _sut = new();

    [Fact]
    public void Validate_EnabledWithDefaults_Succeeds() =>
        _sut.Validate(null, new GranitKeyRotationOptions { Enabled = true })
            .Succeeded.ShouldBeTrue();

    [Fact]
    public void Validate_Disabled_SkipsValidation()
    {
        // Rotation off → the knobs are inert, so even nonsense values must not block startup.
        GranitKeyRotationOptions options = new()
        {
            Enabled = false,
            RsaKeySize = 512,
            GracePeriod = TimeSpan.Zero,
        };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(8192)]
    public void Validate_RsaKeySizeOutOfRange_Fails(int keySize)
    {
        GranitKeyRotationOptions options = new() { Enabled = true, RsaKeySize = keySize };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitKeyRotationOptions.RsaKeySize));
    }

    [Fact]
    public void Validate_NonPositiveGracePeriod_Fails()
    {
        GranitKeyRotationOptions options = new() { Enabled = true, GracePeriod = TimeSpan.Zero };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RotationLeadTimeNotShorterThanKeyLifetime_Fails()
    {
        GranitKeyRotationOptions options = new()
        {
            Enabled = true,
            KeyLifetime = TimeSpan.FromDays(30),
            RotationLeadTime = TimeSpan.FromDays(30),
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitKeyRotationOptions.RotationLeadTime));
    }

    [Fact]
    public void Validate_RefreshCheckIntervalNotBelowGracePeriod_Fails()
    {
        GranitKeyRotationOptions options = new()
        {
            Enabled = true,
            GracePeriod = TimeSpan.FromMinutes(5),
            RefreshCheckInterval = TimeSpan.FromMinutes(5),
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitKeyRotationOptions.RefreshCheckInterval));
    }

    [Fact]
    public void Validate_EmptySigningAlgorithm_Fails()
    {
        GranitKeyRotationOptions options = new() { Enabled = true, SigningAlgorithm = "  " };

        _sut.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_CollectsAllFailures()
    {
        GranitKeyRotationOptions options = new()
        {
            Enabled = true,
            RsaKeySize = 512,
            GracePeriod = TimeSpan.Zero,
            SigningAlgorithm = "",
        };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failures.ShouldNotBeNull();
        result.Failures.Count().ShouldBeGreaterThanOrEqualTo(3);
    }
}
