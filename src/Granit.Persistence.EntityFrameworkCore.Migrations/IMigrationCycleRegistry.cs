namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Registry that maps migration cycle identifiers to their batch delegate and target DbContext type.
/// Registered as a singleton. Thread-safe.
/// </summary>
public interface IMigrationCycleRegistry
{
    /// <summary>
    /// Registers a batch migration delegate for the given cycle identifier.
    /// </summary>
    /// <param name="cycleId">Unique identifier for the migration cycle.</param>
    /// <param name="dbContextType">
    /// The <see cref="Microsoft.EntityFrameworkCore.DbContext"/> subtype to resolve from DI.
    /// </param>
    /// <param name="migration">The delegate that performs one batch of data migration.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a cycle with the same <paramref name="cycleId"/> is already registered.
    /// </exception>
    void Register(string cycleId, Type dbContextType, BatchMigrationDelegate migration);

    /// <summary>
    /// Retrieves the registration for the given cycle identifier, or <c>null</c> if not found.
    /// </summary>
    MigrationCycleRegistration? Find(string cycleId);
}
