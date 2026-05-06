using Granit.Persistence.EntityFrameworkCore.Extensions;
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
    /// Wires the isolated <see cref="TaxonomyDbContext"/> via <c>AddGranitDbContext</c>
    /// (interceptor DI for audit / soft-delete) and registers the EF Core-backed
    /// <c>ITagService</c>. Must be called after
    /// <c>services.AddGranitTaxonomy()</c>.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitTaxonomyEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<TaxonomyDbContext>(configure);
        builder.Services.AddScoped<ITagService, TagService>();
        builder.Services.AddScoped<ITagAssignmentService, TagAssignmentService>();
        builder.Services.AddScoped<ITagSearchService, TagSearchService>();
        builder.Services.AddScoped<ICategoryService, CategoryService>();
        builder.Services.AddScoped<ICategoryAssignmentService, CategoryAssignmentService>();

        return builder;
    }
}
