using Granit.Auditing.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit audit log entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class AuditingModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Auditing module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureAuditingModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntityChangeConfiguration());
        modelBuilder.ApplyConfiguration(new AuditPropertyChangeConfiguration());
        return modelBuilder;
    }
}
