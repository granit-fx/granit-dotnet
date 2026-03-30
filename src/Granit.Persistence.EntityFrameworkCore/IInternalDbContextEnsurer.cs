namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Creates tables for internal Granit <see cref="Microsoft.EntityFrameworkCore.DbContext"/> instances
/// that are not host-migratable (i.e., do not implement <c>IMigratableModule&lt;T&gt;</c>).
/// </summary>
/// <remarks>
/// <para>
/// Most internal DbContexts (BackgroundJobs, Webhooks, etc.) expose a
/// <c>Configure{Module}Module()</c> ModelBuilder extension so the host application includes
/// their tables in its own migrations. However, some contexts — such as
/// <c>OpenIddictDbContext</c> which extends <c>IdentityDbContext</c> — cannot be composed
/// into a host DbContext and need their tables created independently.
/// </para>
/// <para>
/// Implementations are discovered by the migration runner during <c>--migrate</c> and called
/// after host migrations complete, ensuring internal tables exist before data seeding.
/// Register via <c>TryAddEnumerable</c> to support multiple ensurers.
/// </para>
/// </remarks>
public interface IInternalDbContextEnsurer
{
    /// <summary>Display name for logging purposes.</summary>
    string ContextName { get; }

    /// <summary>Creates the internal DbContext tables if they do not already exist.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
