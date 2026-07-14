using Granit.Http.Cookies.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Http.Cookies.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Granit cookie-consent ledger
/// entity configuration in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class CookiesModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Cookies module, emitting the
    /// consent-table DDL from this context's migrations.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Exactly ONE DbContext may own the consent-table DDL.</b> The isolated
    /// <c>CookiesDbContext</c> owns it by default; a host context that also maps the
    /// entity MUST call <see cref="ConfigureCookiesModule(ModelBuilder, bool)"/> with
    /// <c>excludeFromMigrations: true</c>, otherwise both contexts emit
    /// <c>CREATE TABLE</c> for the same table and migration generation crashes.
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureCookiesModule(this ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureCookiesModule(excludeFromMigrations: false);

    /// <summary>
    /// Applies all entity configurations for the Granit Cookies module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="excludeFromMigrations">
    /// <c>true</c> to map the consent entity for querying while emitting no DDL from this
    /// context's migrations; <c>false</c> when this context owns the consent table's schema.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureCookiesModule(this ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new CookieConsentRecordConfiguration(excludeFromMigrations));
        return modelBuilder;
    }
}
