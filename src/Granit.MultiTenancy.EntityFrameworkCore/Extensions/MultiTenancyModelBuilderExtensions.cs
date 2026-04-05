using Granit.MultiTenancy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.MultiTenancy.EntityFrameworkCore;

/// <summary>
/// Extension methods for applying multi-tenancy entity configurations to a <see cref="ModelBuilder"/>.
/// </summary>
public static class MultiTenancyModelBuilderExtensions
{
    /// <summary>
    /// Applies the multi-tenancy module entity configurations.
    /// Called by both the internal <see cref="Internal.MultiTenancyDbContext"/> and host applications
    /// that include multi-tenancy tables in their migrations.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ConfigureMultiTenancyModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantEntityTypeConfiguration());
        return modelBuilder;
    }
}
