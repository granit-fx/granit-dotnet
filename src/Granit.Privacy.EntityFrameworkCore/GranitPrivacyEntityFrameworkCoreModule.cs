using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Privacy.</summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitPrivacyModule))]
public sealed class GranitPrivacyEntityFrameworkCoreModule : GranitModule;
