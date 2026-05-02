using Granit.Entities.Customization.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Entities.Customization.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in
/// <c>Granit.Entities.Customization</c>.
/// </summary>
public static class EntitiesCustomizationEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the isolated <see cref="CustomizationDbContext"/> via
    /// <c>AddGranitDbContext</c> (which wires audit + soft-delete + lifecycle
    /// interceptors automatically) and replaces the default no-op
    /// <see cref="IEntityCustomizationReader"/> with the EF impl. Also
    /// registers the matching <see cref="IEntityCustomizationWriter"/>.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitEntitiesCustomizationEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<CustomizationDbContext>(configure);
        builder.Services.Replace(ServiceDescriptor.Scoped<IEntityCustomizationReader, EfEntityCustomizationReader>());
        builder.Services.AddScoped<IEntityCustomizationWriter, EfEntityCustomizationWriter>();
        return builder;
    }
}
