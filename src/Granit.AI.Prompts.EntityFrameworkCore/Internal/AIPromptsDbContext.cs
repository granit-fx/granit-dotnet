using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.Prompts.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated, tenant-aware DbContext for the prompt catalogue. Inherits
/// <see cref="GranitDbContext"/> so the multi-tenant query filter is parameterised per request.
/// </summary>
internal sealed class AIPromptsDbContext(
    DbContextOptions<AIPromptsDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Prompt templates.</summary>
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ConfigureAIPromptsModule();
    }
}
