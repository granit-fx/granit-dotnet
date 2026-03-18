using Granit.Authentication.ApiKeys.EntityFrameworkCore.EntityConfigurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit API key entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class ApiKeysModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit ApiKeys module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureApiKeysModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ApiKeyEntryConfiguration());
        return modelBuilder;
    }
}
