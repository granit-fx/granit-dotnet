using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Endpoints.Internal;
using Granit.Identity.Endpoints.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Unit tests for <see cref="WebhookSignatureValidator"/>. Header format is
/// Stripe-style: <c>t=&lt;unix-seconds&gt;,v1=&lt;hex&gt;</c>, signed payload is
/// <c>"&lt;unix-seconds&gt;.&lt;body&gt;"</c>.
/// </summary>
public sealed class WebhookSignatureValidatorTests
{
    private const string Secret = "my-webhook-secret";
    private static readonly DateTimeOffset Now = new(2026, 4, 23, 14, 0, 0, TimeSpan.Zero);

    private static (WebhookSignatureValidator Validator, FakeTimeProvider Clock) CreateValidator(
        string secret = Secret,
        TimeSpan? replayWindow = null) =>
        Create(new IdentityWebhookOptions
        {
            Secret = secret,
            ReplayWindow = replayWindow ?? TimeSpan.FromMinutes(5),
        });

    private static (WebhookSignatureValidator Validator, FakeTimeProvider Clock) Create(IdentityWebhookOptions options)
    {
        FakeTimeProvider clock = new(Now);
        WebhookSignatureValidator validator = new(
            Microsoft.Extensions.Options.Options.Create(options),
            clock,
            NullLogger<WebhookSignatureValidator>.Instance);
        return (validator, clock);
    }

    [Fact]
    public void IsEnabled_true_when_secret_configured()
    {
        (WebhookSignatureValidator validator, _) = CreateValidator();
        validator.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_false_when_secret_empty()
    {
        (WebhookSignatureValidator validator, _) = CreateValidator(string.Empty);
        validator.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_true_for_valid_signature()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();
        string signature = ComputeStripeSignature(Secret, clock.GetUtcNow(), payload);

        validator.Validate(payload, signature).ShouldBeTrue();
    }

    [Fact]
    public void Validate_accepts_v1_then_t_order()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();
        long t = clock.GetUtcNow().ToUnixTimeSeconds();
        string hex = ComputeHexHmac(Secret, t, payload);
        string signature = $"v1={hex},t={t}";

        validator.Validate(payload, signature).ShouldBeTrue();
    }

    [Fact]
    public void Validate_returns_false_for_tampered_signature()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();
        string valid = ComputeStripeSignature(Secret, clock.GetUtcNow(), payload);
        string tampered = valid[..^1] + (valid[^1] == 'a' ? 'b' : 'a');

        validator.Validate(payload, tampered).ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_for_tampered_body()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();
        string signature = ComputeStripeSignature(Secret, clock.GetUtcNow(), payload);

        validator.Validate("HELLO"u8.ToArray(), signature).ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_for_null_signature()
    {
        (WebhookSignatureValidator validator, _) = CreateValidator();
        validator.Validate("hello"u8.ToArray(), null).ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_for_malformed_header()
    {
        (WebhookSignatureValidator validator, _) = CreateValidator();
        validator.Validate("hello"u8.ToArray(), "not-a-stripe-format").ShouldBeFalse();
    }

    [Fact]
    public void Validate_returns_false_when_disabled()
    {
        (WebhookSignatureValidator validator, _) = CreateValidator(string.Empty);
        validator.Validate("hello"u8.ToArray(), null).ShouldBeFalse();
    }

    [Fact]
    public void Validate_rejects_signature_outside_replay_window()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();

        // Sign at a timestamp 10 minutes in the past — well beyond the 5-minute window.
        DateTimeOffset signedAt = clock.GetUtcNow().AddMinutes(-10);
        string signature = ComputeStripeSignature(Secret, signedAt, payload);

        validator.Validate(payload, signature).ShouldBeFalse();
    }

    [Fact]
    public void Validate_rejects_signature_in_distant_future()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();

        // Sign at a timestamp 10 minutes in the future — also beyond tolerance.
        DateTimeOffset signedAt = clock.GetUtcNow().AddMinutes(10);
        string signature = ComputeStripeSignature(Secret, signedAt, payload);

        validator.Validate(payload, signature).ShouldBeFalse();
    }

    [Fact]
    public void Validate_accepts_signature_inside_replay_window()
    {
        (WebhookSignatureValidator validator, FakeTimeProvider clock) = CreateValidator();
        byte[] payload = "hello"u8.ToArray();

        // 4 minutes of clock skew — inside the default 5-minute window.
        DateTimeOffset signedAt = clock.GetUtcNow().AddMinutes(-4);
        string signature = ComputeStripeSignature(Secret, signedAt, payload);

        validator.Validate(payload, signature).ShouldBeTrue();
    }

    private static string ComputeStripeSignature(string secret, DateTimeOffset timestamp, byte[] payload)
    {
        long t = timestamp.ToUnixTimeSeconds();
        string hex = ComputeHexHmac(secret, t, payload);
        return $"t={t},v1={hex}";
    }

    private static string ComputeHexHmac(string secret, long timestamp, byte[] payload)
    {
        byte[] key = Encoding.UTF8.GetBytes(secret);
        byte[] prefix = Encoding.UTF8.GetBytes(timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
        byte[] toSign = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, toSign, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, toSign, prefix.Length, payload.Length);
        return Convert.ToHexStringLower(HMACSHA256.HashData(key, toSign));
    }
}
