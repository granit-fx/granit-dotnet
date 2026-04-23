using Granit.DataLookup.EntityFrameworkCore.Extensions;
using Granit.Events.Extensions;
using Granit.MultiTenancy.Domain;
using Granit.MultiTenancy.EntityFrameworkCore.Internal;
using Granit.MultiTenancy.Stores;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.MultiTenancy.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering the multi-tenancy EF Core persistence layer.
/// </summary>
public static class MultiTenancyEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the multi-tenancy EF Core persistence layer.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Delegate to configure the <see cref="DbContextOptionsBuilder"/> (e.g. <c>UseNpgsql</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitMultiTenancyEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<MultiTenancyDbContext>(configure);

        // Ensure event infrastructure is available (fallback if not called directly)
        builder.Services.AddGranitEvents();

        // Replace default (no-op) implementations with EF Core store
        builder.Services.Replace(ServiceDescriptor.Scoped<ITenantReader, EfCoreTenantStore>());
        builder.Services.Replace(ServiceDescriptor.Scoped<ITenantWriter, EfCoreTenantStore>());

        // Replace NullTenantEnumerator with EF Core implementation for per-tenant migrations.
        // Singleton: uses IServiceScopeFactory to resolve scoped ITenantReader on each call.
        builder.Services.Replace(ServiceDescriptor.Singleton<ITenantEnumerator, EfCoreTenantEnumerator>());

        // Queryable source for the Granit query engine (filtering, pagination, sort over Tenant).
        builder.Services.TryAddScoped<IQueryableSource<Tenant>, EfTenantQueryableSource>();

        // Granit.DataLookup source: exposes Tenant as the "tenants" lookup so admin
        // UIs (QueryEngine filter picker + edit-form dropdowns) can pick a tenant by
        // name without typing a GUID. Host-scope only — gated by Platform.Tenants.Read.
        builder.Services.AddQueryableLookup<Tenant, MultiTenancyDbContext>(
            name: "tenants",
            valueSelector: t => t.Id,
            labelSelector: t => t.Name,
            searchPredicate: (t, search) => t.Name.Contains(search) || t.Identifier.Contains(search),
            requiredPermission: "Platform.Tenants.Read");

        return builder;
    }
}
