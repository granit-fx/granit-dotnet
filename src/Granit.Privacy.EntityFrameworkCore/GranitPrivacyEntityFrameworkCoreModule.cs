using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Privacy.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Privacy.</summary>
[DependsOn(
    typeof(GranitPrivacyModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitPrivacyEntityFrameworkCoreModule : GranitModule;
