using Granit.BlobStorage.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit blob storage entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class BlobStorageModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit BlobStorage module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureBlobStorageModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BlobDescriptorConfiguration());
        return modelBuilder;
    }
}
