using Granit.Authentication.OpenIddict.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.OpenIddict.Tests.Options;

public sealed class GranitOpenIddictValidationOptionsValidatorTests
{
    private readonly GranitOpenIddictValidationOptionsValidator _sut = new();

    [Fact]
    public void Validate_HttpsIssuer_Succeeds()
    {
        GranitOpenIddictValidationOptions options = new() { Issuer = new Uri("https://auth.example.com") };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_NullIssuer_Fails()
    {
        ValidateOptionsResult result = _sut.Validate(null, new GranitOpenIddictValidationOptions());

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Issuer");
    }

    [Fact]
    public void Validate_HttpNonLoopbackIssuer_Fails()
    {
        GranitOpenIddictValidationOptions options = new() { Issuer = new Uri("http://auth.example.com") };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("https");
    }

    [Fact]
    public void Validate_HttpLoopbackIssuer_Succeeds()
    {
        // Loopback http is tolerated so a local mock authorization server works in dev/tests.
        GranitOpenIddictValidationOptions options = new() { Issuer = new Uri("http://localhost:5000") };

        _sut.Validate(null, options).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_RelativeIssuer_Fails()
    {
        GranitOpenIddictValidationOptions options = new() { Issuer = new Uri("/auth", UriKind.Relative) };

        ValidateOptionsResult result = _sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("absolute");
    }
}
