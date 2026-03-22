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
    /// Fully-qualified names of all domain base classes in Granit.Core.Domain.
    /// Shared across <see cref="LayerDependencyTests"/> and other convention tests
    /// to avoid repetition.
    /// </summary>
    internal static readonly string[] DomainBaseClassFullNames =
    [
        "Granit.Core.Domain.Entity",
        "Granit.Core.Domain.CreationAuditedEntity",
        "Granit.Core.Domain.AuditedEntity",
        "Granit.Core.Domain.FullAuditedEntity",
        "Granit.Core.Domain.AggregateRoot",
        "Granit.Core.Domain.CreationAuditedAggregateRoot",
        "Granit.Core.Domain.AuditedAggregateRoot",
        "Granit.Core.Domain.FullAuditedAggregateRoot",
    ];
}
