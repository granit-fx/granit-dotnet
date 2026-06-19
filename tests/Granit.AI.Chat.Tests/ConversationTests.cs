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
    public void Create_defaults_to_not_favorite()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        conversation.IsFavorite.ShouldBeFalse();
    }

    [Fact]
    public void SetFavorite_sets_the_explicit_state_and_is_idempotent()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        conversation.SetFavorite(true);
        conversation.IsFavorite.ShouldBeTrue();

        conversation.SetFavorite(true);
        conversation.IsFavorite.ShouldBeTrue();

        conversation.SetFavorite(false);
        conversation.IsFavorite.ShouldBeFalse();
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

    [Fact]
    public void Create_stores_the_workspace_key()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat", "support-chat");

        conversation.WorkspaceKey.ShouldBe("support-chat");
    }

    [Fact]
    public void Create_leaves_workspace_key_null_when_omitted()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        conversation.WorkspaceKey.ShouldBeNull();
    }

    [Fact]
    public void AddMessage_records_the_workspace_key_on_the_message()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        Message message = conversation.AddMessage(Guid.NewGuid(), MessageRole.User, "Hello", "analytics");

        message.WorkspaceKey.ShouldBe("analytics");
    }

    [Fact]
    public void AddMessage_leaves_workspace_key_null_when_omitted()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Owner, "Chat");

        Message message = conversation.AddMessage(Guid.NewGuid(), MessageRole.User, "Hello");

        message.WorkspaceKey.ShouldBeNull();
    }
}
