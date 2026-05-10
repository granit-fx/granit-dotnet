using Granit.Documents.Renditions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including <c>Granit.Documents.Renditions</c>
/// entity configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class RenditionsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the renditions module.</summary>
    public static ModelBuilder ConfigureRenditionsModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new DocumentRenditionConfiguration());
        return modelBuilder;
    }
}
