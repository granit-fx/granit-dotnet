using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Events;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class MessageReportTests
{
    private static readonly Guid Owner = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Create_sets_the_fields_and_raises_the_reported_event()
    {
        var reportId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();

        var report = MessageReport.Create(
            reportId, messageId, conversationId, Owner, "This is wrong.", MessageReportCategory.Inaccurate);

        report.Id.ShouldBe(reportId);
        report.MessageId.ShouldBe(messageId);
        report.ConversationId.ShouldBe(conversationId);
        report.OwnerId.ShouldBe(Owner);
        report.Reason.ShouldBe("This is wrong.");
        report.Category.ShouldBe(MessageReportCategory.Inaccurate);

        ChatMessageReportedEvent raised = report.DomainEvents.OfType<ChatMessageReportedEvent>().ShouldHaveSingleItem();
        raised.ReportId.ShouldBe(reportId);
        raised.MessageId.ShouldBe(messageId);
        raised.ConversationId.ShouldBe(conversationId);
        raised.OwnerId.ShouldBe(Owner);
        raised.Reason.ShouldBe("This is wrong.");
        raised.Category.ShouldBe(MessageReportCategory.Inaccurate);
    }

    [Fact]
    public void Create_allows_a_null_category()
    {
        var report = MessageReport.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Owner, "No category.", category: null);

        report.Category.ShouldBeNull();
        report.DomainEvents.OfType<ChatMessageReportedEvent>().ShouldHaveSingleItem().Category.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_reason(string reason) =>
        Should.Throw<ArgumentException>(() => MessageReport.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Owner, reason, MessageReportCategory.Other));
}
