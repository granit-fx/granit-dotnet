namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// Annotates an EF Core <c>Migration</c> class with its Expand &amp; Contract phase and cycle identifier.
/// Required on any migration that performs a <c>DropColumn</c> or <c>AlterColumn</c> (Contract phase).
/// </summary>
/// <remarks>
/// <example>
/// <code>
/// [MigrationCycle(MigrationPhase.Expand, "patient-fullname-v2")]
/// public partial class AddPatientFullName : Migration { ... }
///
/// [MigrationCycle(MigrationPhase.Contract, "patient-fullname-v2")]
/// public partial class RemovePatientOldColumns : Migration { ... }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MigrationCycleAttribute(MigrationPhase phase, string cycleId) : Attribute
{
    /// <summary>The phase of the Expand &amp; Contract cycle this migration implements.</summary>
    public MigrationPhase Phase { get; } = phase;

    /// <summary>
    /// Unique identifier shared across all migrations belonging to the same cycle
    /// (e.g., <c>"patient-fullname-v2"</c>).
    /// </summary>
    public string CycleId { get; } = cycleId;
}
