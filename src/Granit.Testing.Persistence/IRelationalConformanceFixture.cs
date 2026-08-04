using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Testing.Persistence;

/// <summary>
/// Provider contract for the conformance suites. A provider integration project implements
/// this over a real database container (Testcontainers) and inherits the abstract suites.
/// </summary>
public interface IRelationalConformanceFixture
{
    /// <summary>Human-readable provider name for failure messages (e.g. <c>"PostgreSQL"</c>).</summary>
    string ProviderName { get; }

    /// <summary>Configures the provider on the context options (e.g. <c>UseNpgsql(cs)</c>).</summary>
    void UseProvider(DbContextOptionsBuilder builder);

    /// <summary>
    /// Creates an independent <see cref="IGranitMigrationLock"/> instance wired to the same
    /// database, resolved through the provider package's real DI registration path. Each call
    /// returns a NEW instance so contention tests can pit two "replicas" against each other.
    /// </summary>
    IGranitMigrationLock CreateMigrationLock();
}
