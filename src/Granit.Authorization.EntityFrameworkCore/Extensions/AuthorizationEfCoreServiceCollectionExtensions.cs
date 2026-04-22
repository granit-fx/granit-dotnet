using Granit.Authorization;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Internal;
using Granit.Authorization.EntityFrameworkCore.Stores;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authorization.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core authorization services.
/// </summary>
public static class AuthorizationEfCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers EF Core persistence for permission grants and role metadata.
    /// Replaces the default <see cref="NullPermissionGrantStore"/> and
    /// <see cref="Services.NullRoleMetadataStore"/> registered by <c>Granit.Authorization</c>
    /// with EF-backed implementations. <c>IPermissionManagerReader</c> and
    /// <c>IPermissionManagerWriter</c> are already registered by <c>Granit.Authorization</c>
    /// and delegate to the store.
    /// </summary>
    /// <typeparam name="TContext">
    /// The application DbContext, which must implement <see cref="IPermissionGrantDbContext"/>.
    /// </typeparam>
    public static IServiceCollection AddGranitAuthorizationEntityFrameworkCore<TContext>(
        this IServiceCollection services)
        where TContext : Microsoft.EntityFrameworkCore.DbContext, IPermissionGrantDbContext
    {
        // Replace the NullPermissionGrantStore registered by Granit.Authorization
        services.Replace(ServiceDescriptor.Scoped<IPermissionGrantStore,
            EfCorePermissionGrantStore<TContext>>());

        // Replace the NullRoleMetadataStore registered by Granit.Authorization
        services.Replace(ServiceDescriptor.Scoped<IRoleMetadataStore,
            EfCoreRoleMetadataStore<TContext>>());

        // Factory-style accessor consumed by IGranitRoleOrchestrator to open a
        // fresh host DbContext for the shared-connection transaction path. Uses
        // TryAdd so host apps can override with a custom accessor if needed.
        services.TryAddScoped<IAuthorizationHostDbContextAccessor,
            AuthorizationHostDbContextAccessor<TContext>>();

        return services;
    }
}
