namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Ensures the migration progress tracking table exists in the database.
/// </summary>
/// <remarks>
/// This interface decouples <c>Granit.Persistence.EntityFrameworkCore.Hosting</c> from the internal
/// <c>MigrationProgressDbContext</c>, avoiding <see cref="System.Reflection.ReflectionTypeLoadException"/>
/// when other modules scan assemblies.
/// </remarks>
public interface IMigrationProgressDbEnsurer
{
    /// <summary>
    /// Creates the migration progress tracking table if it does not exist.
    /// </summary>
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);
}
