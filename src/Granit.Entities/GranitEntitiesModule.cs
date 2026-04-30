using Granit.Entities.Extensions;
using Granit.Modularity;

namespace Granit.Entities;

/// <summary>
/// Granit module for the entity-manifest runtime. Hosts the
/// <c>IEntityDefinitionRegistry</c>, the boot-time integrity-check runner
/// (per ADR-040 / story #1541), and the <c>EntitiesOptions</c> binder.
/// </summary>
/// <remarks>
/// <para>
/// Pull this module from a host that resolves the manifest. Pull
/// <see cref="GranitEntitiesAbstractionsModule"/> alone from any base module that
/// only declares an <see cref="EntityDefinition{TEntity}"/> — that way the base
/// module stays free of the runtime registry, hosted service, and DI introspection.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitEntitiesAbstractionsModule))]
public sealed class GranitEntitiesModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitEntities();
}
