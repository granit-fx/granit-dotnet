using Granit.Modularity;
using Granit.Parties.EntityFrameworkCore;

namespace Granit.Parties.Deduplication;

/// <summary>
/// Granit module marker for the Party duplicate-detection 3-tier pipeline. Wired via
/// <c>builder.AddGranitPartiesDeduplication()</c> after the Parties EF Core layer.
/// </summary>
[DependsOn(typeof(GranitPartiesEntityFrameworkCoreModule))]
public sealed class GranitPartiesDeduplicationModule : GranitModule;
