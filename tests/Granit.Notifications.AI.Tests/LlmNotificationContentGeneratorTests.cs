using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Internal;
using Granit.Notifications.AI.Options;
using Granit.Notifications.AI.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Notifications.AI.Tests;

public sealed class LlmNotificationContentGeneratorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly ILogger<LlmNotificationContentGenerator> _logger = NullLogger<LlmNotificationContentGenerator>.Instance;

    private LlmNotificationContentGenerator CreateSut(NotificationsAIOptions? options = null) =>
        new(_structured, MsOptions.Create(options ?? new NotificationsAIOptions()), _logger);

    private static NotificationDeliveryContext MakeContext(
        string typeName = "order.completed",
        NotificationSeverity severity = NotificationSeverity.Info,
        string? culture = "en") =>
        new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = typeName,
            Severity = severity,
            RecipientUserId = "user-123",
            Data = JsonSerializer.SerializeToElement(new { OrderId = "ORD-001", Total = 99.99 }),
            OccurredAt = new DateTimeOffset(2026, 3, 16, 14, 0, 0, TimeSpan.Zero),
            Culture = culture,
        };

    private static StructuredCompletionResult<NotificationContentResponse> Ok(string subject, string body) =>
        new()
        {
            Status = StructuredCompletionStatus.Succeeded,
            Value = new NotificationContentResponse { Subject = subject, Body = body },
        };

    private void Respond(StructuredCompletionResult<NotificationContentResponse> result) =>
        _structured
            .CompleteAsync<NotificationContentResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task GenerateAsync_ValidResponse_ReturnsContent()
    {
        Respond(Ok("Order Completed", "Your order ORD-001 has been completed."));

        NotificationContent? result = await CreateSut().GenerateAsync(MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Subject.ShouldBe("Order Completed");
        result.Body.ShouldBe("Your order ORD-001 has been completed.");
    }

    [Theory]
    [InlineData(StructuredCompletionStatus.ModelRefused)]
    [InlineData(StructuredCompletionStatus.SchemaViolation)]
    [InlineData(StructuredCompletionStatus.TransportFailure)]
    public async Task GenerateAsync_NonSuccessStatus_ReturnsNull(StructuredCompletionStatus status)
    {
        Respond(new StructuredCompletionResult<NotificationContentResponse> { Status = status });

        NotificationContent? result = await CreateSut().GenerateAsync(MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_BlankSubject_ReturnsNull()
    {
        Respond(Ok("", "Some body"));

        NotificationContent? result = await CreateSut().GenerateAsync(MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_BlankBody_ReturnsNull()
    {
        Respond(Ok("Some subject", "   "));

        NotificationContent? result = await CreateSut().GenerateAsync(MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_RoutesDataAsContentAndTypeCultureAsInstructionContext()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<NotificationContentResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok("S", "B"));

        // Forwarding the Data payload is an explicit GDPR opt-in.
        await CreateSut(new NotificationsAIOptions { AllowPersonalDataInPrompts = true }).GenerateAsync(
            MakeContext(typeName: "user.registered", culture: "fr"), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Untrusted business data flows through the Content channel...
        captured.Content.ShouldContain("ORD-001");
        // ...the locale is developer-controlled instruction...
        captured.Instruction!.ShouldContain("fr");
        // ...and the notification type travels as labelled context.
        captured.Context.ShouldNotBeNull();
        captured.Context.ShouldContain(kv => kv.Value == "user.registered");
    }

    [Fact]
    public async Task GenerateAsync_ByDefault_DoesNotForwardDataPayloadToTheModel()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<NotificationContentResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok("S", "B"));

        await CreateSut().GenerateAsync(MakeContext(), TestContext.Current.CancellationToken);

        // GDPR: the payload may contain personal data — it must never reach the LLM
        // unless the host explicitly sets AllowPersonalDataInPrompts = true.
        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("{}");
        captured.Content.ShouldNotContain("ORD-001");
    }
}
