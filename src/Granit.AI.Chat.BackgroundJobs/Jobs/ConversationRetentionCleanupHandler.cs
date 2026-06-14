using System.Diagnostics.CodeAnalysis;

namespace Granit.AI.Chat.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="ConversationRetentionCleanupJob"/>. Delegates to
/// <see cref="IConversationRetentionService"/> for the purge logic.
/// </summary>
[SuppressMessage("Major Code Smell", "S1118:Utility classes should not have public constructors", Justification = "Wolverine message handler — public class with public static Handle method is required for discovery (CLAUDE.md).")]
public class ConversationRetentionCleanupHandler
{
    public static Task HandleAsync(
        ConversationRetentionCleanupJob _,
        IConversationRetentionService service,
        CancellationToken cancellationToken) =>
        service.ExecuteAsync(cancellationToken);
}
