using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Unit tests for <see cref="WebhookSignatureValidator"/>.
/// </summary>
public sealed class WebhookSignatureValidatorTests
{
    private const string Secret = "my-webhook-secret";

    private static WebhookSignatureValidator CreateValidator(string secret = Secret) =>
        new(Microsoft.Extensions.Options.Options.Create(new IdentityWebhookOptions { Secret = secret }),
            NullLogger<WebhookSignatureValidator>.Instance);

    [Fact]
    public void IsEnabled_true_when_secret_configured()
    {
        WebhookSignatureValidator validator = CreateValidator();
        validator.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_false_when_secret_empty()
    {
        WebhookSignatureValidator validator = CreateValidator(string.Empty);
        validator.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_true_for_valid_signature()
    {
        WebhookSignatureValidator validator = CreateValidator();
        byte[] payload = "hello"u8.ToArray();
        string signature = ComputeHmac(Secret, payload);

        validator.Validate(payload, signature).ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_false_for_invalid_signature()
    {
        WebhookSignatureValidator validator = CreateValidator();
        byte[] payload = "hello"u8.ToArray();

        validator.Validate(payload, "bad-signature").ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_for_null_signature()
    {
        WebhookSignatureValidator validator = CreateValidator();
        byte[] payload = "hello"u8.ToArray();

        validator.Validate(payload, null).ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_when_disabled()
    {
        WebhookSignatureValidator validator = CreateValidator(string.Empty);
        byte[] payload = "hello"u8.ToArray();

        validator.Validate(payload, null).ShouldBeFalse();
    }

    private static string ComputeHmac(string secret, byte[] payload)
    {
        byte[] key = Encoding.UTF8.GetBytes(secret);
        byte[] hash = HMACSHA256.HashData(key, payload);
        return Convert.ToHexStringLower(hash);
    }
}
