using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Endpoints.Validators;
using Granit.Authorization;
using Granit.Localization;
using Shouldly;

namespace Granit.AI.Chat.Endpoints.Tests;

public sealed class ConversationEndpointsUnitTests
{
    private sealed class CapturingPermissionContext : IPermissionDefinitionContext
    {
        public List<PermissionGroup> Groups { get; } = [];

        public PermissionGroup AddGroup(string name, LocalizableString? displayName = null)
        {
            PermissionGroup group = new(name, displayName);
            Groups.Add(group);
            return group;
        }
    }

    [Fact]
    public void Permission_provider_declares_read_manage_delete()
    {
        CapturingPermissionContext context = new();

        new AIChatPermissionDefinitionProvider().DefinePermissions(context);

        PermissionGroup group = context.Groups.ShouldHaveSingleItem();
        group.Name.ShouldBe("AIChat");
        group.Permissions.Select(p => p.Name).ShouldBe(
            [
                "AIChat.Conversations.Read",
                "AIChat.Conversations.Send",
                "AIChat.Conversations.Manage",
                "AIChat.Conversations.Delete",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Send_validator_rejects_blank_and_overlong_messages()
    {
        SendMessageRequestValidator validator = new();

        validator.Validate(new SendMessageRequest("")).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest(new string('x', 16001))).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest("What changed last week?")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Create_validator_rejects_blank_and_overlong_titles()
    {
        CreateConversationRequestValidator validator = new();

        validator.Validate(new CreateConversationRequest("")).IsValid.ShouldBeFalse();
        validator.Validate(new CreateConversationRequest(new string('x', 501))).IsValid.ShouldBeFalse();
        validator.Validate(new CreateConversationRequest("Daily brief")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Rename_validator_rejects_blank_titles()
    {
        RenameConversationRequestValidator validator = new();

        validator.Validate(new RenameConversationRequest("  ")).IsValid.ShouldBeFalse();
        validator.Validate(new RenameConversationRequest("Renamed")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Response_projects_the_aggregate_with_messages()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Guid.NewGuid(), "Chat");
        conversation.AddMessage(Guid.NewGuid(), MessageRole.Assistant, "Hi");

        var response = ConversationResponse.FromAggregate(conversation);

        response.Title.ShouldBe("Chat");
        response.OwnerId.ShouldBe(conversation.OwnerId);
        MessageResponse message = response.Messages.ShouldHaveSingleItem();
        message.Role.ShouldBe("Assistant");
        message.Content.ShouldBe("Hi");
    }
}
