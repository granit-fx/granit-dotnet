using Granit.AI.Chat.BackgroundJobs.Internal;
using Granit.AI.Chat.BackgroundJobs.Options;
using Granit.BackgroundJobs;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat.BackgroundJobs;

/// <summary>
/// Granit module that registers the distributed conversation-retention cleanup recurring job
/// (ADR-067, GDPR data minimisation). Retention is opt-in via
/// <see cref="GranitAIChatRetentionOptions.RetentionDays"/>.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitBackgroundJobsModule))]
public sealed class GranitAIChatBackgroundJobsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services
            .AddOptions<GranitAIChatRetentionOptions>()
            .BindConfiguration(GranitAIChatRetentionOptions.SectionName);

        context.Services.TryAddTransient<IConversationRetentionService, ConversationRetentionService>();
    }
}
