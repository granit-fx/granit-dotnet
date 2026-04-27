using Granit.Mergeable.EntityFrameworkCore;
using Granit.Modularity;

namespace Granit.Parties.Mergeable;

/// <summary>
/// Granit module marker that wires <see cref="Granit.Parties.Domain.Party"/> into the merge
/// orchestrator. Wired via <c>builder.AddGranitPartiesMergeable()</c> after
/// <c>AddGranitPartiesEntityFrameworkCore</c> + <c>AddGranitMergeableEntityFrameworkCore</c>.
/// </summary>
[DependsOn(typeof(GranitMergeableEntityFrameworkCoreModule))]
public sealed class GranitPartiesMergeableModule : GranitModule;
