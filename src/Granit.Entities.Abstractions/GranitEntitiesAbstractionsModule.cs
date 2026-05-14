using Granit.Modularity;

namespace Granit.Entities;

/// <summary>
/// Granit module marker for the entity manifest abstractions package.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so a base module can declare
/// <c>[DependsOn(typeof(GranitEntitiesAbstractionsModule))]</c> and reference the
/// <see cref="EntityDefinition{TEntity}"/> hierarchy (plus <see cref="EntityEndpointMetadata"/>)
/// without taking a runtime dependency on the <c>Granit.Entities</c> registry,
/// integrity-check runner, or the <c>Granit.Entities.Endpoints</c> HTTP layer.
/// </remarks>
public sealed class GranitEntitiesAbstractionsModule : GranitModule;
