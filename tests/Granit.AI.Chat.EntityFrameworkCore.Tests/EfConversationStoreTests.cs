using Granit.AI.Chat.Domain;
using Granit.AI.Chat.EntityFrameworkCore.Internal;
using Shouldly;

namespace Granit.AI.Chat.EntityFrameworkCore.Tests;

public sealed class EfConversationStoreTests : IDisposable
{
    private static readonly Guid UserA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfConversationStore _sut;

    public EfConversationStoreTests() => _sut = new EfConversationStore(_factory);

    public void Dispose() => _factory.Dispose();

    private async Task<Conversation> SeedAsync(Guid owner, string title, params string[] messages)
    {
        var conversation = Conversation.Create(Guid.NewGuid(), owner, title);
        foreach (string text in messages)
        {
            conversation.AddMessage(Guid.NewGuid(), MessageRole.User, text);
        }

        return await _sut.CreateAsync(conversation, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Get_returns_the_owner_conversation_with_messages()
    {
        Conversation seeded = await SeedAsync(UserA, "Brief", "hi", "there");

        Conversation? loaded = await _sut.GetAsync(seeded.Id, UserA, TestContext.Current.CancellationToken);

        loaded.ShouldNotBeNull();
        loaded.Title.ShouldBe("Brief");
        loaded.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Get_does_not_return_another_users_conversation()
    {
        Conversation seeded = await SeedAsync(UserA, "Private");

        Conversation? loaded = await _sut.GetAsync(seeded.Id, UserB, TestContext.Current.CancellationToken);

        loaded.ShouldBeNull();
    }

    [Fact]
    public async Task List_returns_only_the_owners_conversations()
    {
        await SeedAsync(UserA, "A1");
        await SeedAsync(UserA, "A2");
        await SeedAsync(UserB, "B1");

        IReadOnlyList<Conversation> result = await _sut.ListAsync(UserA, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(c => c.OwnerId == UserA);
    }

    [Fact]
    public async Task Rename_succeeds_for_the_owner_and_fails_for_others()
    {
        Conversation seeded = await SeedAsync(UserA, "Old");

        (await _sut.RenameAsync(seeded.Id, UserB, "Hijacked", TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _sut.RenameAsync(seeded.Id, UserA, "New", TestContext.Current.CancellationToken)).ShouldBeTrue();

        Conversation? loaded = await _sut.GetAsync(seeded.Id, UserA, TestContext.Current.CancellationToken);
        loaded!.Title.ShouldBe("New");
    }

    [Fact]
    public async Task AppendMessages_adds_to_the_owner_conversation_and_rejects_others()
    {
        Conversation seeded = await SeedAsync(UserA, "Chat", "first");

        Message[] toAppend =
        [
            Message.Create(Guid.NewGuid(), seeded.Id, MessageRole.User, "second"),
            Message.Create(Guid.NewGuid(), seeded.Id, MessageRole.Assistant, "answer"),
        ];

        (await _sut.AppendMessagesAsync(seeded.Id, UserB, toAppend, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _sut.AppendMessagesAsync(seeded.Id, UserA, toAppend, TestContext.Current.CancellationToken)).ShouldBeTrue();

        Conversation? loaded = await _sut.GetAsync(seeded.Id, UserA, TestContext.Current.CancellationToken);
        loaded!.Messages.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ReportMessage_persists_a_report_for_the_owner()
    {
        Conversation seeded = await SeedAsync(UserA, "Chat", "hello");
        Guid messageId = seeded.Messages[0].Id;

        bool reported = await _sut.ReportMessageAsync(
            Guid.NewGuid(), messageId, UserA, "Wrong answer", MessageReportCategory.Inaccurate, TestContext.Current.CancellationToken);

        reported.ShouldBeTrue();

        await using AIChatDbContext context = _factory.CreateDbContext();
        MessageReport stored = context.MessageReports.Single();
        stored.MessageId.ShouldBe(messageId);
        stored.ConversationId.ShouldBe(seeded.Id);
        stored.OwnerId.ShouldBe(UserA);
        stored.Reason.ShouldBe("Wrong answer");
        stored.Category.ShouldBe(MessageReportCategory.Inaccurate);
    }

    [Fact]
    public async Task ReportMessage_rejects_a_message_in_another_users_conversation()
    {
        Conversation seeded = await SeedAsync(UserA, "Private", "secret");
        Guid messageId = seeded.Messages[0].Id;

        bool reported = await _sut.ReportMessageAsync(
            Guid.NewGuid(), messageId, UserB, "Probing", MessageReportCategory.Other, TestContext.Current.CancellationToken);

        reported.ShouldBeFalse();

        await using AIChatDbContext context = _factory.CreateDbContext();
        context.MessageReports.ShouldBeEmpty();
    }

    [Fact]
    public async Task ReportMessage_returns_false_when_the_message_is_unknown()
    {
        await SeedAsync(UserA, "Chat", "hello");

        bool reported = await _sut.ReportMessageAsync(
            Guid.NewGuid(), Guid.NewGuid(), UserA, "No such message", category: null, TestContext.Current.CancellationToken);

        reported.ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_succeeds_for_the_owner_and_fails_for_others()
    {
        Conversation seeded = await SeedAsync(UserA, "Doomed");

        (await _sut.DeleteAsync(seeded.Id, UserB, TestContext.Current.CancellationToken)).ShouldBeFalse();
        (await _sut.DeleteAsync(seeded.Id, UserA, TestContext.Current.CancellationToken)).ShouldBeTrue();

        (await _sut.GetAsync(seeded.Id, UserA, TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
