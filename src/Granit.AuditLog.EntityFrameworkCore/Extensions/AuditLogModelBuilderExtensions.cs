using Granit.AuditLog.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.AuditLog.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit audit log entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class AuditLogModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit AuditLog module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureAuditLogModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditLogEntryConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntityChangeConfiguration());
        modelBuilder.ApplyConfiguration(new AuditPropertyChangeConfiguration());
        return modelBuilder;
    }
}
