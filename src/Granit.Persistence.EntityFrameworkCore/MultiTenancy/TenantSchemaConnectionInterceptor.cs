using System.Data.Common;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// EF Core connection interceptor that activates the current tenant's schema
/// immediately after each physical connection is opened, delegating provider-specific
/// SQL to <see cref="ITenantSchemaActivator"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Connection pool safety (critical)</strong> — Database drivers (Npgsql, etc.)
/// do not close physical connections after use: they return them to the connection pool.
/// A connection that previously served tenant A retains the previous schema setting.
/// If tenant B acquires that connection and this interceptor did not run, tenant B
/// would read tenant A's data — a catastrophic ISO 27001 data breach.
/// </para>
/// <para>
/// To prevent this, both <see cref="ConnectionOpened"/> and <see cref="ConnectionOpenedAsync"/>
/// invoke <see cref="ITenantSchemaActivator"/> <em>unconditionally</em> on every lease
/// from the pool. There is no cache, no "already set" check, and no escape condition.
/// </para>
/// <para>
/// When <see cref="ICurrentTenant.IsAvailable"/> is <c>false</c> (no active tenant),
/// the schema is left unchanged — the connection uses the database default.
/// This is intentional for host-level maintenance connections that operate outside
/// any tenant context.
/// </para>
/// </remarks>
internal sealed class TenantSchemaConnectionInterceptor(
    ICurrentTenant currentTenant,
    ITenantSchemaProvider schemaProvider,
    ITenantSchemaActivator schemaActivator) : DbConnectionInterceptor
{
    /// <inheritdoc/>
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        if (!currentTenant.IsAvailable)
        {
            return;
        }

        // Synchronous path: convert to Task before blocking to satisfy CA2012.
        // DefaultTenantSchemaProvider returns a completed ValueTask so AsTask() is allocation-free.
        // Custom implementations should avoid async I/O in GetSchemaNameAsync when called
        // from the synchronous EF Core path.
        string schema = schemaProvider
            .GetSchemaNameAsync(currentTenant.Id!.Value)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        schemaActivator.ActivateSchema(connection, schema);
    }

    /// <inheritdoc/>
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!currentTenant.IsAvailable)
        {
            return;
        }

        string schema = await schemaProvider
            .GetSchemaNameAsync(currentTenant.Id!.Value, cancellationToken)
            .ConfigureAwait(false);

        await schemaActivator.ActivateSchemaAsync(connection, schema, cancellationToken)
            .ConfigureAwait(false);
    }
}
