// =============================================================================
// Tests - NoOpWebhookSecretProtector
// =============================================================================
// Verifies that the pass-through protector returns secrets unchanged.
// =============================================================================

using Granit.Webhooks.Internal;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class NoOpWebhookSecretProtectorTests
{
    private readonly NoOpWebhookSecretProtector _protector = new();

    [Fact]
    public async Task ProtectAsync_ReturnsPlainSecretUnchanged()
    {
        const string secret = "test-webhook-value-256";

        string result = await _protector.ProtectAsync(secret, TestContext.Current.CancellationToken);

        result.ShouldBe(secret);
    }

    [Fact]
    public async Task UnprotectAsync_ReturnsProtectedSecretUnchanged()
    {
        const string secret = "my-protected-secret";

        string result = await _protector.UnprotectAsync(secret, TestContext.Current.CancellationToken);

        result.ShouldBe(secret);
    }

    [Fact]
    public async Task ProtectAsync_EmptyString_ReturnsEmptyString()
    {
        string result = await _protector.ProtectAsync(string.Empty, TestContext.Current.CancellationToken);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task UnprotectAsync_EmptyString_ReturnsEmptyString()
    {
        string result = await _protector.UnprotectAsync(string.Empty, TestContext.Current.CancellationToken);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task RoundTrip_ProtectThenUnprotect_ReturnsSameValue()
    {
        const string original = "round-trip-secret";

        string protectedValue = await _protector.ProtectAsync(original, TestContext.Current.CancellationToken);
        string unprotected = await _protector.UnprotectAsync(protectedValue, TestContext.Current.CancellationToken);

        unprotected.ShouldBe(original);
    }
}
