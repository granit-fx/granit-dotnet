using Granit.AI.Prompts.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Prompts.EntityFrameworkCore.Extensions;

/// <summary>Registers the AI Prompts EF Core persistence.</summary>
public static class AIPromptsEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the isolated <c>AIPromptsDbContext</c> (honouring the tenant-isolation strategy)
    /// and the <see cref="IPromptTemplateStore"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureShared">EF Core options for the shared-database strategy (required).</param>
    /// <param name="configureDatabasePerTenant">Optional database-per-tenant connection override.</param>
    /// <param name="configureSchemaPerTenant">Optional schema-per-tenant options override.</param>
    /// <param name="configureTenantSchema">Optional tenant-schema tuning.</param>
    public static IServiceCollection AddGranitAIPromptsEntityFrameworkCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureShared,
        Action<DbContextOptionsBuilder, string>? configureDatabasePerTenant = null,
        Action<DbContextOptionsBuilder>? configureSchemaPerTenant = null,
        Action<TenantSchemaOptions>? configureTenantSchema = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureShared);

        services.AddGranitIsolatedDbContext<AIPromptsDbContext>(
            configureShared,
            configureDatabasePerTenant,
            configureSchemaPerTenant,
            configureTenantSchema);

        services.TryAddScoped<IPromptTemplateStore, EfPromptTemplateStore>();
        services.TryAddScoped<IPromptCategoryStore, EfPromptCategoryStore>();

        return services;
    }
}
