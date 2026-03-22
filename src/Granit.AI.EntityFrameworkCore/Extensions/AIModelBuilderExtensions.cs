using Granit.AI.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit AI entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>Not dead code — called in <c>AIDbContext.OnModelCreating</c> to apply entity configurations.</remarks>
public static class AIModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit AI module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureAIModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AIWorkspaceEntityConfiguration());
        modelBuilder.ApplyConfiguration(new AIUsageRecordEntityConfiguration());
        return modelBuilder;
    }
}
