using Granit.AI.Chat.Domain;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class ConversationTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_sets_owner_and_title()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Daily brief");

        conversation.OwnerId.ShouldBe(Owner);
        conversation.Title.ShouldBe("Daily brief");
        conversation.Messages.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_title(string title) =>
        Should.Throw<ArgumentException>(() => Conversation.Create(Guid.NewGuid(), Owner, title));

    [Fact]
    public void Rename_changes_the_title()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Old");

        conversation.Rename("New");

        conversation.Title.ShouldBe("New");
    }

    [Fact]
    public void Rename_rejects_blank_title()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Old");

        Should.Throw<ArgumentException>(() => conversation.Rename(" "));
    }

    [Fact]
    public void AddMessage_appends_a_message_bound_to_the_conversation()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        Message message = conversation.AddMessage(Guid.NewGuid(), MessageRole.User, "Hello");

        conversation.Messages.ShouldHaveSingleItem().ShouldBeSameAs(message);
        message.ConversationId.ShouldBe(conversation.Id);
        message.Role.ShouldBe(MessageRole.User);
        message.Content.ShouldBe("Hello");
    }
}
