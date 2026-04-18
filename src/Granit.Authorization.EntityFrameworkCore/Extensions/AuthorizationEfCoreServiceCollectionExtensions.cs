using Granit.Authorization;
using Granit.Authorization.EntityFrameworkCore.DbContext;
using Granit.Authorization.EntityFrameworkCore.Entities;
using Granit.Authorization.EntityFrameworkCore.Exports;
using Granit.Authorization.EntityFrameworkCore.Queries;
using Granit.Authorization.EntityFrameworkCore.Stores;
using Granit.DataExchange.Extensions;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authorization.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core authorization services.
/// </summary>
public static class AuthorizationEfCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers EF Core persistence for permission grants.
    /// Replaces the default <see cref="NullPermissionGrantStore"/> registered by
    /// <c>Granit.Authorization</c> with <see cref="EfCorePermissionGrantStore{TContext}"/>.
    /// <c>IPermissionManagerReader</c> and <c>IPermissionManagerWriter</c> are already registered
    /// by <c>Granit.Authorization</c> and delegate to the store.
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

        // Query + Export definitions (ADR-020: owned by the base module).
        services.AddQueryDefinition<PermissionGrant, PermissionGrantQueryDefinition>();
        services.AddExportDefinition<PermissionGrant, PermissionGrantExportDefinition>();

        return services;
    }
}
