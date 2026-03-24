using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Features.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of feature value overrides.
/// Registers <see cref="Internal.GranitFeaturesDbContext"/> and <see cref="Internal.EfCoreFeatureStore"/>.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitFeaturesEntityFrameworkCore(opt =>
///     opt.UseNpgsql(connectionString));
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitFeaturesModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitFeaturesEntityFrameworkCoreModule : GranitModule;
