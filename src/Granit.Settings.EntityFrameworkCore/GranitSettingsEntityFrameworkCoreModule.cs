using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Settings.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of settings backed by the dedicated
/// <c>SettingsDbContext</c>.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitSettingsEntityFrameworkCore(opts =>
///     opts.Configure = db => db.UseNpgsql(connectionString));
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitSettingsModule))]
public sealed class GranitSettingsEntityFrameworkCoreModule : GranitModule;
