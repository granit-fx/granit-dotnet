using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Encryption.ReEncryption.Extensions;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for Granit re-encryption.
/// </summary>
public static class ReEncryptionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IReEncryptionJob"/> backed by <see cref="DefaultReEncryptionJob{TContext}"/>.
    /// </summary>
    /// <typeparam name="TContext">
    /// The <see cref="DbContext"/> that owns the entities to re-encrypt.
    /// An <see cref="IDbContextFactory{TContext}"/> must already be registered
    /// (via <c>AddDbContextFactory&lt;TContext&gt;()</c>).
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitEncryptionReEncryption<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        services.AddScoped<IReEncryptionJob, DefaultReEncryptionJob<TContext>>();
        return services;
    }
}
