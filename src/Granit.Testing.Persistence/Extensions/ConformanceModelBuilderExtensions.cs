using Granit.Testing.Persistence.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.Persistence.Extensions;

/// <summary>
/// ModelBuilder extension for the conformance entities, following the standard
/// <c>Configure{Module}Module()</c> pattern so a consuming context can embed them.
/// </summary>
public static class ConformanceModelBuilderExtensions
{
    /// <summary>
    /// Registers the conformance entities. All Granit conventions (filters, enum-as-string,
    /// concurrency token, audit columns) are applied by <c>ApplyGranitConventions</c> /
    /// <c>GranitDbContext</c> — this method only declares the entity set and column widths.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureConformanceModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConformanceOrder>(order =>
            order.Property(o => o.Label).HasMaxLength(128));

        modelBuilder.Entity<ConformanceToggle>(toggle =>
            toggle.Property(t => t.Label).HasMaxLength(128));

        return modelBuilder;
    }
}
