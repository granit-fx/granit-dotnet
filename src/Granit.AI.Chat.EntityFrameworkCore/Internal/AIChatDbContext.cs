using Granit.AI.Chat.Domain;
using Granit.AI.Chat.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated, tenant-aware DbContext for chat conversations. Inherits <see cref="GranitDbContext"/>
/// so the multi-tenant query filter is parameterised per request.
/// </summary>
internal sealed class AIChatDbContext(
    DbContextOptions<AIChatDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Conversations.</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureAIChatModule();
    }
}
