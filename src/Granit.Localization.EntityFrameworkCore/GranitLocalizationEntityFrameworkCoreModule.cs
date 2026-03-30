using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Localization.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of localization overrides.
/// Registers <see cref="Internal.LocalizationDbContext"/> and
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
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitLocalizationEntityFrameworkCoreModule : GranitModule;
