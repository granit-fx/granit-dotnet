using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of managed hostnames.
/// Registers <c>HostnamesDbContext</c>, <c>EfManagedHostnameStore</c> and <c>EfHostnameResolver</c>.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitHostnamesEntityFrameworkCore(opt =>
///     opt.UseNpgsql(connectionString));
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitHostnamesModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitHostnamesEntityFrameworkCoreModule : GranitModule;
