using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Associates a migration cycle identifier with the application's tenant
/// <see cref="DbContext"/> type and its batch migration logic.
/// </summary>
/// <param name="CycleId">Unique identifier for the migration cycle.</param>
/// <param name="DbContextType">
/// The <see cref="DbContext"/> subtype to resolve from DI for the batch delegate.
/// </param>
/// <param name="Migration">The delegate that performs one batch of data migration.</param>
public sealed record MigrationCycleRegistration(
    string CycleId,
    Type DbContextType,
    BatchMigrationDelegate Migration);
