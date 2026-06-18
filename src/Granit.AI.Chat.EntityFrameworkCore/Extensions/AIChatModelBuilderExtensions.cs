using Granit.AI.Chat.EntityFrameworkCore.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore.Extensions;

/// <summary>
/// Applies the AI Chat entity configurations. The host owns migrations and can call this from a
/// shared DbContext if it does not use the isolated one.
/// </summary>
public static class AIChatModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit AI Chat module.</summary>
    public static ModelBuilder ConfigureAIChatModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new MessageConfiguration());
        modelBuilder.ApplyConfiguration(new MessageReportConfiguration());
        return modelBuilder;
    }
}
