namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Creates tables for host-level internal <see cref="Microsoft.EntityFrameworkCore.DbContext"/>
/// instances (BackgroundJobs, BFF, MultiTenancy, Features, OpenIddict, Auditing, Localization).
/// </summary>
/// <remarks>
/// Host ensurers create tables in the host schema (or provider default) once,
/// regardless of tenant context. Discovered and executed by the migration runner
/// during <c>--migrate</c> before data seeding.
/// </remarks>
public interface IHostInternalDbContextEnsurer
{
    /// <summary>Display name for logging purposes.</summary>
    string ContextName { get; }

    /// <summary>Creates the host DbContext tables if they do not already exist.</summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
