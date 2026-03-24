using Granit.Modularity;
using Granit.Persistence;

namespace Granit.Settings.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of settings.
/// Registers <see cref="Internal.EfCoreSettingStore{TDbContext}"/> via
/// <c>builder.AddGranitSettingsEfCore&lt;TDbContext&gt;()</c>.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitSettingsEfCore&lt;AppDbContext&gt;();
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitSettingsModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitSettingsEntityFrameworkCoreModule : GranitModule;
