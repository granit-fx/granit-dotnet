using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Localization.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of localization overrides.
/// Registers <see cref="Internal.GranitLocalizationOverridesDbContext"/> and
/// <see cref="Internal.EfCoreLocalizationOverrideStore"/> wrapped by <c>CachedLocalizationOverrideStore</c>.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitLocalizationEntityFrameworkCore(opt =>
///     opt.UseYourProvider(connectionString));
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitLocalizationModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitLocalizationEntityFrameworkCoreModule : GranitModule;
