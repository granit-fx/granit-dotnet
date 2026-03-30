namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Enumerates the active tenant identifiers for use during migration startup.
/// </summary>
/// <remarks>
/// <para>
/// Behavior varies by multi-tenancy topology:
/// </para>
/// <list type="table">
///   <listheader><term>Topology</term><description>Expected behavior</description></listheader>
///   <item>
///     <term>Single-tenant</term>
///     <description>
///     Register the default <c>NullTenantEnumerator</c> (no values yielded).
///     <c>MigrationStartupService</c> uses the <c>TenantId</c> stored in the progress row
///     (which will be <c>null</c>, mapped to <see cref="Guid.Empty"/>).
///     </description>
///   </item>
///   <item>
///     <term>Shared database (TenantId column)</term>
///     <description>
///     Register the default <c>NullTenantEnumerator</c> (no values yielded).
///     <c>MigrationStartupService</c> uses the <c>TenantId</c> already stored in each
///     <c>MigrationProgress</c> row.
///     </description>
///   </item>
///   <item>
///     <term>Tenant-per-Schema or Tenant-per-Database</term>
///     <description>
///     Register a custom implementation that yields all active tenant identifiers
///     from the application's tenant registry. <c>MigrationStartupService</c> will
///     publish one <c>RunMigrationBatchCommand</c> per tenant per pending cycle.
///     </description>
///   </item>
/// </list>
/// <para>
/// Register a custom implementation before calling <c>AddGranitPersistenceMigrations()</c>:
/// <code>
/// builder.Services.AddSingleton&lt;ITenantEnumerator, MyTenantEnumerator&gt;();
/// builder.AddGranitPersistenceMigrations(...);
/// </code>
/// </para>
/// </remarks>
public interface ITenantEnumerator
{
    /// <summary>
    /// Returns the identifiers of all currently active tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An async stream of <see cref="Guid"/> tenant identifiers.
    /// Returns an empty stream for the default <c>NullTenantEnumerator</c>.
    /// </returns>
    IAsyncEnumerable<Guid> GetActiveTenantIdsAsync(CancellationToken cancellationToken);
}
