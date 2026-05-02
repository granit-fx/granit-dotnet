using Granit.Entities.Endpoints;
using Granit.Entities.EntityFrameworkCore.Extensions;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Entities.EntityFrameworkCore;

/// <summary>
/// EF Core companion module for <c>Granit.Entities.Endpoints</c>. Wires the calendar
/// range executor that turns the declarative <c>CalendarLayoutDescriptor</c> into a
/// real <c>Where().Select().ToListAsync()</c> against an EF Core <c>DbContext</c> via
/// <see cref="QueryEngine.IQueryableSource{TEntity}"/>.
/// </summary>
/// <remarks>
/// <para>
/// Depends on <c>GranitPersistenceEntityFrameworkCoreModule</c> per the framework
/// rule that every <c>*.EntityFrameworkCore</c> package declares the dependency, even
/// when (as here) the package owns no <c>DbContext</c> of its own — it only consumes
/// hosts' <c>IQueryableSource&lt;T&gt;</c> registrations.
/// </para>
/// <para>
/// The module replaces the framework's default <c>NullCalendarRangeService</c>; the
/// per-entity dispatcher registry is built lazily at request time from the runners
/// the <see cref="EntitiesEntityFrameworkCoreServiceCollectionExtensions.AddGranitEntitiesEntityFrameworkCore"/>
/// extension registered upstream.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEntitiesEndpointsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitEntitiesEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntitiesEntityFrameworkCore();
}
