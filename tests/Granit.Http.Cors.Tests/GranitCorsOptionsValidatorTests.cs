using Granit.Http.Cors.Internal;
using Granit.Http.Cors.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.Cors.Tests;

public sealed class GranitCorsOptionsValidatorTests
{
    [Fact]
    public void Validate_Production_WithExplicitOrigins_ReturnsSuccess()
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = ["https://app.example.com"] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Production_WithWildcardOrigin_ReturnsFailed()
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = ["*"] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("wildcard");
    }

    [Fact]
    public void Validate_Staging_WithWildcardOrigin_ReturnsFailed()
    {
        IHostEnvironment environment = CreateEnvironment("Staging");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = ["*"] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Development_WithWildcardOrigin_ReturnsSuccess()
    {
        IHostEnvironment environment = CreateEnvironment("Development");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = ["*"] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_EmptyOrigins_ReturnsFailed()
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = [] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitCorsOptions.AllowedOrigins));
    }

    [Fact]
    public void Validate_AllowCredentialsWithWildcard_ReturnsFailed()
    {
        IHostEnvironment environment = CreateEnvironment("Development");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new()
        {
            AllowedOrigins = ["*"],
            AllowCredentials = true,
        };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(nameof(GranitCorsOptions.AllowCredentials));
    }

    [Fact]
    public void Validate_AllowCredentialsWithExplicitOrigins_ReturnsSuccess()
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new()
        {
            AllowedOrigins = ["https://app.example.com"],
            AllowCredentials = true,
        };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Production_WildcardAndCredentials_ReportsMultipleErrors()
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new()
        {
            AllowedOrigins = ["*"],
            AllowCredentials = true,
        };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures.Count().ShouldBeGreaterThanOrEqualTo(2);
    }

    // Origin format validation with auto-trim for trailing slash

    [Theory]
    [InlineData("https://app.example.com")]
    [InlineData("https://app.example.com/")]     // trailing slash silently normalized
    [InlineData("https://app.example.com:8443")]
    [InlineData("http://localhost:3000")]
    public void Validate_WellFormedOrigin_ReturnsSuccess(string origin)
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = [origin] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Succeeded.ShouldBeTrue(
            $"'{origin}' is a well-formed origin (trailing slash is tolerated).");
    }

    [Theory]
    [InlineData("app.example.com")]                   // missing scheme
    [InlineData("ftp://app.example.com")]             // wrong scheme
    [InlineData("https://app.example.com/login")]     // path
    [InlineData("https://app.example.com?foo=bar")]   // query
    [InlineData("https://app.example.com#fragment")]  // fragment
    public void Validate_MalformedOrigin_ReturnsFailed(string origin)
    {
        IHostEnvironment environment = CreateEnvironment("Production");
        GranitCorsOptionsValidator sut = new(environment);
        GranitCorsOptions options = new() { AllowedOrigins = [origin] };

        ValidateOptionsResult result = sut.Validate(null, options);

        result.Failed.ShouldBeTrue($"'{origin}' is not a valid CORS origin.");
        result.FailureMessage.ShouldContain(origin);
    }

    [Fact]
    public void NormalizedOrigins_TrailingSlash_IsStripped()
    {
        GranitCorsOptions options = new()
        {
            AllowedOrigins = ["https://app.example.com/", "https://admin.example.com"],
        };

        options.NormalizedOrigins.ShouldBe(["https://app.example.com", "https://admin.example.com"]);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        IHostEnvironment environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }
}
