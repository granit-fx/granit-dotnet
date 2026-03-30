using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.SavedViews;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.QueryEngine.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the QueryEngine EF Core persistence layer on <see cref="IHostApplicationBuilder"/>.
/// </summary>
public static class QueryEngineEfCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the QueryEngine EF Core persistence layer, including the isolated
    /// <c>QueryEngineDbContext</c> and <see cref="ISavedViewStoreReader"/>/<see cref="ISavedViewStoreWriter"/> implementation.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure the database provider (e.g. <c>opts.UseNpgsql(cs)</c>).</param>
    /// <returns>The host application builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitQueryEngineEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<QueryEngineDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<QueryEngineDbContext>();

        // Replace the null-object default from Granit.QueryEngine — CQRS forwarding pattern
        builder.Services.AddScoped<EfCoreSavedViewStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<ISavedViewStoreReader>(sp => sp.GetRequiredService<EfCoreSavedViewStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<ISavedViewStoreWriter>(sp => sp.GetRequiredService<EfCoreSavedViewStore>()));

        return builder;
    }
}
