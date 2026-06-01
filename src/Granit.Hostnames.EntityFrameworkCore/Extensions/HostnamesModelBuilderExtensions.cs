using Granit.Hostnames.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit hostname entity configurations
/// in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class HostnamesModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Hostnames module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureHostnamesModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ManagedHostnameConfiguration());
        return modelBuilder;
    }
}
