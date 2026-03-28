using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit templating entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class TemplatingModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Templating module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureTemplatingModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TemplateRevisionEntityConfiguration());
        modelBuilder.ApplyConfiguration(new TemplateCategoryEntityConfiguration());
        return modelBuilder;
    }
}
