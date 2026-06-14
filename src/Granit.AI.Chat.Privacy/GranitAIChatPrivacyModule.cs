using Granit.AI.Chat.Privacy.DataExport;
using Granit.Modularity;
using Granit.Privacy.BlobStorage;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.Privacy;

/// <summary>
/// Granit module that registers the <see cref="ConversationPrivacyDataProvider"/> so chat
/// conversations participate in the privacy export scatter-gather saga (GDPR Art. 15/20). The
/// matching export handler and the personal-data deletion handler are discovered automatically by
/// Wolverine assembly scanning. Apps opt in via
/// <c>AddGranitPrivacy(p =&gt; p.AddGranitAIChatPrivacyProvider())</c>.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitPrivacyBlobStorageModule))]
public sealed class GranitAIChatPrivacyModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<ConversationPrivacyDataProvider>();
}
