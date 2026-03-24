using Granit.ArchitectureTests.Abstractions;

namespace Granit.ArchitectureTests;

/// <summary>
/// Loads the entire Granit architecture graph once per test run.
/// All test classes share this static instance to avoid repeated assembly scanning.
/// </summary>
internal static class GranitArchitecture
{
    internal static readonly ArchUnitNET.Domain.Architecture Instance =
        ArchitectureLoader.Load("Granit.", typeof(GranitArchitecture).Assembly);

    /// <summary>
    /// Fully-qualified names of all domain base classes in Granit.Domain.
    /// Shared across <see cref="LayerDependencyTests"/> and other convention tests
    /// to avoid repetition.
    /// </summary>
    internal static readonly string[] DomainBaseClassFullNames =
    [
        "Granit.Domain.Entity",
        "Granit.Domain.CreationAuditedEntity",
        "Granit.Domain.AuditedEntity",
        "Granit.Domain.FullAuditedEntity",
        "Granit.Domain.AggregateRoot",
        "Granit.Domain.CreationAuditedAggregateRoot",
        "Granit.Domain.AuditedAggregateRoot",
        "Granit.Domain.FullAuditedAggregateRoot",
    ];
}
