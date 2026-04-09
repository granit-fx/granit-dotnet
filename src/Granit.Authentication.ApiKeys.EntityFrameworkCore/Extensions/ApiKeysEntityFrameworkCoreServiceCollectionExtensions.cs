using Granit.Authentication.ApiKeys.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.ApiKeys.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods to register API key EF Core services.
/// </summary>
public static class ApiKeysEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the EF Core API key store and <see cref="Internal.AuthenticationApiKeysDbContext"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDbContext">Action to configure the <see cref="Internal.AuthenticationApiKeysDbContext"/> options (e.g., connection string).</param>
    public static IServiceCollection AddGranitApiKeysEntityFrameworkCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddGranitDbContext<AuthenticationApiKeysDbContext>(configureDbContext);
        services.AddTenantInternalDbContextEnsurer<AuthenticationApiKeysDbContext>();

        services.TryAddScoped<IApiKeyStore, EfCoreApiKeyStore>();
        services.TryAddScoped<IApiKeyAdminStore, EfCoreApiKeyAdminStore>();

        return services;
    }
}
