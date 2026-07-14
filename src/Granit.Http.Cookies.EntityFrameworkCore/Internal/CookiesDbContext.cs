using Granit.Http.Cookies.Domain;
using Granit.Http.Cookies.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore.Internal;

/// <summary>
/// Isolated EF Core DbContext for the cookie-consent ledger.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CookieConsentRecord"/> is <c>IMultiTenant</c>, so this context inherits
/// <see cref="GranitDbContext"/> — the base installs the parameterised multi-tenant query
/// filter (<c>@ef_filter__CurrentTenantId</c>) and applies the Granit conventions
/// (enum-as-string, audit interceptor support) after <see cref="OnGranitModelCreating"/>.
/// </para>
/// <para>
/// This context owns the consent-table DDL by default; a host context that maps the
/// entity via <c>ConfigureCookiesModule(excludeFromMigrations: true)</c> shares the
/// table without emitting duplicate migrations. Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class CookiesDbContext(
    DbContextOptions<CookiesDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter)
{
    /// <summary>Append-only cookie-consent decisions.</summary>
    public DbSet<CookieConsentRecord> ConsentRecords => Set<CookieConsentRecord>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ConfigureCookiesModule();
}
