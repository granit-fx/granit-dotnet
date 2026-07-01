using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.AI.Chat.Privacy.Tests;

public sealed class ConversationPrivacyDataProviderTests
{
    private static readonly Guid Subject = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly IConversationStore _store = Substitute.For<IConversationStore>();
    private readonly IConversationDataStore _dataManager = Substitute.For<IConversationDataStore>();
    private readonly IStagedFragmentBuilder _fragmentBuilder = Substitute.For<IStagedFragmentBuilder>();

    private ConversationPrivacyDataProvider CreateProvider() => new(_store, _dataManager, _fragmentBuilder);

    private static PrivacyExportContext Context() =>
        new(Guid.NewGuid(), Subject, Subject, TenantId: null, Regulation: "GDPR");

    [Fact]
    public void Provider_metadata_identifies_the_ai_chat_scope()
    {
        ConversationPrivacyDataProvider.ProviderName.ShouldBe("ai-chat");
        ConversationPrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.AIChat");
    }

    [Fact]
    public async Task HasData_is_true_when_the_subject_has_conversations()
    {
        _store.ListAsync(Subject, Arg.Any<CancellationToken>())
            .Returns([Conversation.Create(Guid.NewGuid(), Subject, "t")]);

        (await CreateProvider().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasData_is_true_when_the_subject_has_reports_but_no_conversations()
    {
        _store.ListAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);
        _dataManager.GetReportsForOwnerAsync(Subject, Arg.Any<CancellationToken>())
            .Returns([MessageReport.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Subject, "wrong", MessageReportCategory.Inaccurate)]);

        (await CreateProvider().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task HasData_is_false_when_the_subject_has_no_conversations_or_reports()
    {
        _store.ListAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);
        _dataManager.GetReportsForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);

        (await CreateProvider().HasDataAsync(Context(), TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_yields_nothing_when_there_are_no_conversations()
    {
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([]);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment fragment in CreateProvider().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            fragments.Add(fragment);
        }

        fragments.ShouldBeEmpty();
        await _fragmentBuilder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_builds_one_json_fragment_with_the_conversations()
    {
        var conversation = Conversation.Create(Guid.NewGuid(), Subject, "Brief");
        conversation.AddMessage(Guid.NewGuid(), MessageRole.User, "hi");
        _dataManager.GetAllForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([conversation]);

        await foreach (ExportFragment _ in CreateProvider().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            // drain
        }

        await _fragmentBuilder.Received(1).BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            "ai-chat",
            "ai-chat-conversations.json",
            Arg.Is<ConversationsExport>(d => d.ConversationCount == 1 && d.Conversations[0].Messages.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Export_includes_the_subjects_message_reports()
    {
        var report = MessageReport.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Subject, "This is wrong.", MessageReportCategory.Inaccurate);
        _dataManager.GetReportsForOwnerAsync(Subject, Arg.Any<CancellationToken>()).Returns([report]);

        await foreach (ExportFragment _ in CreateProvider().ExportAsync(Context(), TestContext.Current.CancellationToken))
        {
            // drain
        }

        await _fragmentBuilder.Received(1).BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            "ai-chat",
            "ai-chat-conversations.json",
            Arg.Is<ConversationsExport>(d =>
                d.ReportCount == 1
                && d.Reports[0].Reason == "This is wrong."
                && d.Reports[0].Category == "Inaccurate"),
            Arg.Any<CancellationToken>());
    }
}
