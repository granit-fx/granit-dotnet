using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Taxonomy.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for <c>Granit.Taxonomy</c>.
/// </summary>
public static class TaxonomyEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for <c>Granit.Taxonomy</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wires <see cref="TaxonomyDbContext"/> via <c>AddGranitIsolatedDbContext</c>
    /// — Taxonomy is tenant-only and inherits the active multi-tenancy isolation strategy
    /// (audit + soft-delete interceptors wired automatically).
    /// </para>
    /// <para>Must be called after <c>services.AddGranitTaxonomy()</c>.</para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy.</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant configuration.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant configuration.</param>
    /// <param name="configureTenantSchema">Optional <see cref="TenantSchemaOptions"/> tuning.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitTaxonomyEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configureShared);

        builder.Services.AddGranitIsolatedDbContext<TaxonomyDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);
        builder.Services.AddScoped<ITagService, TagService>();
        builder.Services.AddScoped<ITagAssignmentService, TagAssignmentService>();
        builder.Services.AddScoped<ITagSearchService, TagSearchService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<ICategoryAssignmentService, CategoryAssignmentService>();
        builder.Services.AddScoped<IOrphanAssignmentSweepService, OrphanAssignmentSweepService>();

        return builder;
    }
}
