using System;
using Granit.Documents.Renditions.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Extensions;

/// <summary>Extensions for registering EF Core persistence for <c>Granit.Documents.Renditions</c>.</summary>
public static class RenditionsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the renditions <c>IRenditionStore</c> backed by <see cref="RenditionsDbContext"/>
    /// and wires the cascade handler on <c>DocumentPermanentlyDeletedEvent</c>.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitDocumentsEntityFrameworkCore</c> so that
    /// <c>ITenantQuotaService</c> is registered — this module decrements the
    /// rendition counter through it on cascade.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitDocumentsRenditionsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<RenditionsDbContext>(configure);
        builder.Services.TryAddScoped<IRenditionStore, RenditionStore>();
        builder.Services.TryAddScoped<IRenditionService, RenditionService>();

        return builder;
    }
}
