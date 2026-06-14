using Granit.AI.Chat.Privacy.DataExport;
using Granit.Privacy;
using Granit.Privacy.BlobStorage.Extensions;

namespace Granit.AI.Chat.Privacy.Extensions;

/// <summary>
/// <see cref="GranitPrivacyBuilder"/> extensions that register the AI chat privacy provider.
/// </summary>
public static class PrivacyBuilderAIChatExtensions
{
    /// <summary>
    /// Registers <see cref="ConversationPrivacyDataProvider"/> as an <c>IPrivacyDataProvider</c> and
    /// adds <c>"ai-chat"</c> to the scatter-gather export registry. Wires the
    /// <c>Granit.Privacy.BlobStorage</c> staging infrastructure (idempotent). The matching export
    /// handler is discovered automatically; the deletion handler likewise. Requires
    /// <c>GranitAIChatModule</c> (and its EF Core data manager) to be loaded.
    /// </summary>
    public static GranitPrivacyBuilder AddGranitAIChatPrivacyProvider(this GranitPrivacyBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddGranitPrivacyBlobStorage();
        return builder.AddDataProvider<ConversationPrivacyDataProvider>();
    }
}
