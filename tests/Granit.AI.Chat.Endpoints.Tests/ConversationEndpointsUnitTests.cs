using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Endpoints.Dtos;
using Granit.AI.Chat.Endpoints.Permissions;
using Granit.AI.Chat.Endpoints.Validators;
using Granit.AI.Chat.Options;
using Granit.Authorization;
using Granit.Localization;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Chat.Endpoints.Tests;

public sealed class ConversationEndpointsUnitTests
{
    private static SendMessageRequestValidator CreateSendValidator() =>
        new(MsOptions.Create(new GranitAIChatAttachmentOptions()));

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
        SendMessageRequestValidator validator = CreateSendValidator();

        validator.Validate(new SendMessageRequest("")).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest(new string('x', 16001))).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest("What changed last week?")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Send_validator_rejects_blank_mention_fields_and_too_many()
    {
        SendMessageRequestValidator validator = CreateSendValidator();

        validator.Validate(new SendMessageRequest("hi", Mentions: [new MentionRequest("", "1")])).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest("hi", Mentions: [new MentionRequest("invoice", "")])).IsValid.ShouldBeFalse();
        validator.Validate(new SendMessageRequest("hi", Mentions: [new MentionRequest("invoice", "42")])).IsValid.ShouldBeTrue();

        var tooMany = Enumerable.Range(0, SendMessageRequestValidator.MaxMentions + 1)
            .Select(i => new MentionRequest("invoice", i.ToString()))
            .ToList();
        validator.Validate(new SendMessageRequest("hi", Mentions: tooMany)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Send_validator_enforces_attachment_type_size_and_count_limits()
    {
        var limits = new GranitAIChatAttachmentOptions();
        SendMessageRequestValidator validator = CreateSendValidator();

        // Supported type within size → valid.
        validator.Validate(new SendMessageRequest("hi",
            Attachments: [new AttachmentRequest("blob-1", "a.pdf", "application/pdf", 1024)])).IsValid.ShouldBeTrue();

        // Unsupported content type → rejected.
        validator.Validate(new SendMessageRequest("hi",
            Attachments: [new AttachmentRequest("blob-1", "a.exe", "application/x-msdownload", 1024)])).IsValid.ShouldBeFalse();

        // Over the size limit → rejected.
        validator.Validate(new SendMessageRequest("hi",
            Attachments: [new AttachmentRequest("blob-1", "a.pdf", "application/pdf", limits.MaxAttachmentBytes + 1)])).IsValid.ShouldBeFalse();

        // Blank reference / file name → rejected.
        validator.Validate(new SendMessageRequest("hi",
            Attachments: [new AttachmentRequest("", "a.pdf", "application/pdf", 1024)])).IsValid.ShouldBeFalse();

        // Too many → rejected.
        var tooMany = Enumerable.Range(0, limits.MaxAttachments + 1)
            .Select(i => new AttachmentRequest($"blob-{i}", "a.pdf", "application/pdf", 1024))
            .ToList();
        validator.Validate(new SendMessageRequest("hi", Attachments: tooMany)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Send_validator_rejects_too_many_prompt_refs()
    {
        SendMessageRequestValidator validator = CreateSendValidator();

        validator.Validate(new SendMessageRequest("hi", PromptRefs: [Guid.NewGuid()])).IsValid.ShouldBeTrue();

        var tooMany = Enumerable.Range(0, SendMessageRequestValidator.MaxPromptRefs + 1)
            .Select(_ => Guid.NewGuid())
            .ToList();
        validator.Validate(new SendMessageRequest("hi", PromptRefs: tooMany)).IsValid.ShouldBeFalse();
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
        message.Role.ShouldBe("assistant");
        message.Content.ShouldBe("Hi");
    }
}
