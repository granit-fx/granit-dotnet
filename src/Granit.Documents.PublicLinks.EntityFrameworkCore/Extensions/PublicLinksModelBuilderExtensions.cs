using Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including
/// <c>Granit.Documents.PublicLinks</c> entity configurations in a host-owned
/// <see cref="DbContext"/>.
/// </summary>
public static class PublicLinksModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the public-links module.</summary>
    public static ModelBuilder ConfigurePublicLinksModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new DocumentPublicLinkConfiguration());
        return modelBuilder;
    }
}
