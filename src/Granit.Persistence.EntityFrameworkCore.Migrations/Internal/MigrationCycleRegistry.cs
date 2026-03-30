using System.Collections.Concurrent;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Internal;

/// <summary>
/// Thread-safe singleton implementation of <see cref="IMigrationCycleRegistry"/>.
/// </summary>
internal sealed class MigrationCycleRegistry : IMigrationCycleRegistry
{
    private readonly ConcurrentDictionary<string, MigrationCycleRegistration> _registrations =
        new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public void Register(string cycleId, Type dbContextType, BatchMigrationDelegate migration)
    {
        if (!_registrations.TryAdd(cycleId, new MigrationCycleRegistration(cycleId, dbContextType, migration)))
        {
            throw new InvalidOperationException(
                $"A migration cycle with id '{cycleId}' is already registered.");
        }
    }

    /// <inheritdoc/>
    public MigrationCycleRegistration? Find(string cycleId) =>
        _registrations.TryGetValue(cycleId, out MigrationCycleRegistration? registration)
            ? registration
            : null;
}
