using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Parties.</summary>
[DependsOn(
    typeof(GranitPartiesModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitPartiesEntityFrameworkCoreModule : GranitModule;
