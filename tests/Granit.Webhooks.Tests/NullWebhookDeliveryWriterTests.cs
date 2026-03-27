// =============================================================================
// Tests - NullWebhookDeliveryWriter
// =============================================================================
// Verifies that the no-op writer completes without error for all operations.
// =============================================================================

using System.Text.Json;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class NullWebhookDeliveryWriterTests
{
    private readonly NullWebhookDeliveryWriter _writer = new();

    [Fact]
    public async Task RecordSuccessAsync_CompletesWithoutError()
    {
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => _writer.RecordSuccessAsync(
            command, 200, 42, "hash", null, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RecordSuccessAsync_WithPayload_CompletesWithoutError()
    {
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => _writer.RecordSuccessAsync(
            command, 201, 100, "hash", "{\"data\":true}", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RecordFailureAsync_CompletesWithoutError()
    {
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => _writer.RecordFailureAsync(
            command, 500, 150, "Server Error", null, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RecordFailureAsync_NullStatusCode_CompletesWithoutError()
    {
        SendWebhookCommand command = BuildCommand();

        Func<Task> act = () => _writer.RecordFailureAsync(
            command, null, 0, "Timeout", null, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task SuspendSubscriptionAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _writer.SuspendSubscriptionAsync(
            Guid.NewGuid(), "Auto-suspended: HTTP 401", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SendWebhookCommand BuildCommand() => new()
    {
        DeliveryId = Guid.NewGuid(),
        SubscriptionId = Guid.NewGuid(),
        TargetUrl = "https://example.com/webhook",
        Envelope = new WebhookEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = "test.event",
            TenantId = null,
            Timestamp = DateTimeOffset.UtcNow,
            ApiVersion = "2025-01-01",
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        },
    };
}
