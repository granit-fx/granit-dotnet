using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.Hosting;

/// <summary>
/// Non-generic base for <see cref="IMigratableModule{TContext}"/>.
/// Exposes the <see cref="DbContext"/> type without requiring generic type arguments at the
/// call site — used by <see cref="IGranitMigrationRunner"/> for discovery.
/// </summary>
public interface IMigratableModule
{
    /// <summary>The <see cref="DbContext"/> type that owns EF Core migrations for this module.</summary>
    Type DbContextType { get; }
}

/// <summary>
/// Marker interface for Granit modules that own a migratable <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on your <see cref="Granit.Core.Modularity.GranitModule"/> subclass
/// to declare that the module owns a host <see cref="DbContext"/> with EF Core migrations.
/// The <see cref="IGranitMigrationRunner"/> discovers all modules implementing this interface
/// in topological order (respecting <c>[DependsOn]</c>) and calls
/// <see cref="DatabaseFacade.MigrateAsync(System.Threading.CancellationToken)"/> on each.
/// </para>
/// <para>
/// Internal Granit DbContexts (e.g., <c>BackgroundJobsDbContext</c>) do NOT implement this
/// interface — only host application DbContexts that own migration files should.
/// </para>
/// </remarks>
/// <typeparam name="TContext">The <see cref="DbContext"/> type that owns EF Core migrations.</typeparam>
/// <example>
/// <code>
/// [DependsOn(typeof(GranitPersistenceModule))]
/// public sealed class GuavaCoreModule : GranitModule, IMigratableModule&lt;CoreDbContext&gt;
/// {
///     public override void ConfigureServices(ServiceConfigurationContext context) { /* ... */ }
/// }
/// </code>
/// </example>
public interface IMigratableModule<TContext> : IMigratableModule where TContext : DbContext
{
    Type IMigratableModule.DbContextType => typeof(TContext);
}
