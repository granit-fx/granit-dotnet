using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Extensions;

/// <summary>
/// Extension methods for <see cref="IMigrationCycleRegistry"/> that provide a fluent,
/// strongly-typed registration API.
/// </summary>
public static class MigrationCycleRegistryExtensions
{
    /// <summary>
    /// Registers a batch migration delegate for the given cycle identifier,
    /// inferring the <see cref="DbContext"/> type from the generic parameter.
    /// </summary>
    /// <typeparam name="TContext">The <see cref="DbContext"/> subtype to resolve from DI.</typeparam>
    /// <param name="registry">The migration cycle registry.</param>
    /// <param name="cycleId">Unique identifier for the migration cycle.</param>
    /// <param name="migration">The delegate that performs one batch of data migration.</param>
    /// <returns>The registry, for fluent chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a cycle with the same <paramref name="cycleId"/> is already registered.
    /// </exception>
    public static IMigrationCycleRegistry Register<TContext>(
        this IMigrationCycleRegistry registry,
        string cycleId,
        BatchMigrationDelegate migration)
        where TContext : DbContext
    {
        registry.Register(cycleId, typeof(TContext), migration);
        return registry;
    }
}
