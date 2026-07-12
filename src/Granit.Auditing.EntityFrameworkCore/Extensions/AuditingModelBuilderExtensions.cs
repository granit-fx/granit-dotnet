using Granit.Auditing.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Auditing.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit audit log entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Mapping the audit entities into an audited host context enables <b>embedded audit
/// persistence</b>: the <c>AuditingChangeTrackingInterceptor</c> writes the audit rows in
/// the same transaction as the business mutation, making the trail atomic with the
/// operation it audits. Without the mapping, audit rows are written post-commit through
/// the isolated <c>AuditingDbContext</c> (standalone mode — not atomic).
/// </remarks>
public static class AuditingModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Auditing module, emitting the
    /// audit-table DDL from this context's migrations.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Exactly ONE DbContext may own the audit-table DDL.</b> If a second context also
    /// maps the audit entities with this overload, both emit <c>CREATE TABLE</c> for the same
    /// tables and migration generation or deployment <b>crashes</b> (duplicate/existing
    /// tables). Every additional audited context MUST call
    /// <see cref="ConfigureAuditingModule(ModelBuilder, bool)"/> with
    /// <c>excludeFromMigrations: true</c>.
    /// <code>
    /// // Primary context — owns the audit DDL:
    /// modelBuilder.ConfigureAuditingModule();
    ///
    /// // Every other audited context — maps the tables, emits NO DDL:
    /// modelBuilder.ConfigureAuditingModule(excludeFromMigrations: true);
    /// </code>
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureAuditingModule(this ModelBuilder modelBuilder) =>
        modelBuilder.ConfigureAuditingModule(excludeFromMigrations: false);

    /// <summary>
    /// Applies all entity configurations for the Granit Auditing module.
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>Exactly ONE DbContext may own the audit-table DDL</b> (call with
    /// <c>excludeFromMigrations: false</c> — or the parameterless overload — on that context
    /// only). Every additional audited context must pass <c>excludeFromMigrations: true</c>,
    /// otherwise migration generation or deployment crashes on duplicate audit tables.
    /// </remarks>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="excludeFromMigrations">
    /// <c>true</c> to map the audit entities for querying/embedded writes while emitting no
    /// DDL from this context's migrations; <c>false</c> when this context owns the audit
    /// tables' schema.
    /// </param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureAuditingModule(this ModelBuilder modelBuilder, bool excludeFromMigrations)
    {
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration(excludeFromMigrations));
        modelBuilder.ApplyConfiguration(new AuditEntityChangeConfiguration(excludeFromMigrations));
        modelBuilder.ApplyConfiguration(new AuditPropertyChangeConfiguration(excludeFromMigrations));
        return modelBuilder;
    }
}
