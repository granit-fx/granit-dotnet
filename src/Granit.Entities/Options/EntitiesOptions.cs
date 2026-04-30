namespace Granit.Entities.Options;

/// <summary>
/// Configuration for the <c>Granit.Entities</c> runtime.
/// </summary>
public sealed class EntitiesOptions
{
    /// <summary>
    /// How the boot-time integrity check reacts to unresolved references in any
    /// registered <see cref="EntityDefinition{TEntity}"/>. Default: <see cref="IntegrityCheckMode.Throw"/>.
    /// </summary>
    public IntegrityCheckMode IntegrityCheck { get; set; } = IntegrityCheckMode.Throw;
}
