using System.Runtime.CompilerServices;
using Granit.AI.Chat.Domain;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.AI.Chat.Privacy.DataExport;

/// <summary>
/// Privacy data provider for Granit.AI.Chat. Emits the user's conversations (with their messages)
/// as a single staged JSON fragment during the scatter-gather export saga (GDPR Art. 15/20).
/// Attachment <em>content</em> is not held by the chat module — it is transient and owned by the
/// application's blob store, which contributes its own provider.
/// </summary>
public sealed class ConversationPrivacyDataProvider(
    IConversationStore store,
    IConversationDataManager dataManager,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "ai-chat";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.AIChat";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        // Cheap presence check — the summary list omits messages.
        IReadOnlyList<Conversation> summaries = await store
            .ListAsync(context.SubjectUserId, cancellationToken).ConfigureAwait(false);
        return summaries.Count > 0;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> ExportAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExportCoreAsync(context, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> ExportCoreAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<Conversation> conversations = await dataManager
            .GetAllForOwnerAsync(context.SubjectUserId, cancellationToken).ConfigureAwait(false);
        if (conversations.Count == 0)
        {
            yield break;
        }

        var export = new ConversationsExport(
            UserId: context.SubjectUserId,
            ConversationCount: conversations.Count,
            Conversations: [.. conversations.Select(Map)]);

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "ai-chat-conversations.json", export, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ConversationExport Map(Conversation conversation) =>
        new(
            conversation.Id,
            conversation.Title,
            conversation.CreatedAt,
            [.. conversation.Messages.Select(m => new MessageExport(m.Role.ToString().ToLowerInvariant(), m.Content, m.CreatedAt))]);
}

internal sealed record ConversationsExport(
    Guid UserId,
    int ConversationCount,
    IReadOnlyList<ConversationExport> Conversations);

internal sealed record ConversationExport(
    Guid Id,
    string Title,
    DateTimeOffset CreatedAt,
    IReadOnlyList<MessageExport> Messages);

internal sealed record MessageExport(
    string Role,
    string Content,
    DateTimeOffset CreatedAt);
