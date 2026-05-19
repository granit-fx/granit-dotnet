using FluentValidation.TestHelper;
using Granit.Validation;
using Granit.Webhooks.Endpoints.Validators;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Endpoints.Tests.Validators;

internal sealed record TestUrlModel(string Url);

internal sealed class TestUrlValidator : GranitValidator<TestUrlModel>
{
    public TestUrlValidator()
    {
        RuleFor(x => x.Url).IsValidWebhookTargetUrl();
    }
}

public sealed class WebhookTargetUrlValidatorExtensionsTests
{
    private readonly TestUrlValidator _validator = new();

    [Fact]
    public void Validate_ValidHttpsUrl_ShouldPass()
    {
        var model = new TestUrlModel("https://example.com/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_ValidPublicHostname_ShouldPass()
    {
        var model = new TestUrlModel("https://hooks.myapp.io/events");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_EmptyUrl_ShouldFail(string? url)
    {
        var model = new TestUrlModel(url!);

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_HttpUrl_ShouldFail()
    {
        var model = new TestUrlModel("http://example.com/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_UrlTooLong_ShouldFail()
    {
        string longUrl = $"https://example.com/{new string('a', 2048)}";
        var model = new TestUrlModel(longUrl);

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_Localhost_ShouldFail()
    {
        var model = new TestUrlModel("https://localhost/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_LoopbackIpv4_ShouldFail()
    {
        var model = new TestUrlModel("https://127.0.0.1/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Theory]
    [InlineData("https://10.0.0.1/webhook")]
    [InlineData("https://172.16.0.1/webhook")]
    [InlineData("https://192.168.1.1/webhook")]
    public void Validate_PrivateIpv4_ShouldFail(string url)
    {
        var model = new TestUrlModel(url);

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_AwsMetadataEndpoint_ShouldFail()
    {
        var model = new TestUrlModel("https://169.254.169.254/latest/meta-data/");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_Ipv6Loopback_ShouldFail()
    {
        var model = new TestUrlModel("https://[::1]/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_Ipv4MappedIpv6Loopback_ShouldFail()
    {
        var model = new TestUrlModel("https://[::ffff:127.0.0.1]/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_LinkLocalIpv6_ShouldFail()
    {
        var model = new TestUrlModel("https://[fe80::1]/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Fact]
    public void Validate_UniqueLocalIpv6_ShouldFail()
    {
        var model = new TestUrlModel("https://[fc00::1]/webhook");

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }

    [Theory]
    [InlineData("https://myhost.local/webhook")]
    [InlineData("https://myhost.internal/webhook")]
    [InlineData("https://myhost.localhost/webhook")]
    [InlineData("https://myhost.onion/webhook")]
    public void Validate_BlockedTld_ShouldFail(string url)
    {
        var model = new TestUrlModel(url);

        TestValidationResult<TestUrlModel> result = _validator.TestValidate(model);

        result.ShouldHaveValidationErrorFor(x => x.Url);
    }
}
