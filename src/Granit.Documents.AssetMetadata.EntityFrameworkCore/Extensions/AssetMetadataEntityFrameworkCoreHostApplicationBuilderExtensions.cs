using System;
using Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.AssetMetadata</c>.</summary>
public static class AssetMetadataEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers <see cref="IAssetMetadataStore"/> backed by
    /// <see cref="AssetMetadataDbContext"/> and the cascade handler on
    /// <c>DocumentPermanentlyDeletedEvent</c>.
    /// </summary>
    /// <remarks>
    /// Should be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so the
    /// parent documents <c>DbContext</c> and events are already wired.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitDocumentsAssetMetadataEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<AssetMetadataDbContext>(configure);
        builder.Services.TryAddScoped<IAssetMetadataStore, AssetMetadataStore>();

        return builder;
    }
}
