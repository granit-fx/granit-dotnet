using Granit.Identity.EntityFrameworkCore.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Granit Identity
/// entity configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class IdentityModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Identity module
    /// (currently the <see cref="Granit.Identity.Domain.User"/> aggregate
    /// configured in <see cref="UserConfiguration"/>).
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureGranitIdentityModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        return modelBuilder;
    }
}
