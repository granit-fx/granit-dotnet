using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Encryption.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for Granit re-encryption.
/// </summary>
public static class ReEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IReEncryptionService"/> backed by <see cref="DefaultReEncryptionService{TContext}"/>.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <see cref="DbContext"/> that owns the entities to re-encrypt.
    /// An <see cref="IDbContextFactory{TContext}"/> must already be registered
    /// by the consuming module.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEncryptionReEncryption<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IReEncryptionService, DefaultReEncryptionService<TContext>>();
        return services;
    }
}
