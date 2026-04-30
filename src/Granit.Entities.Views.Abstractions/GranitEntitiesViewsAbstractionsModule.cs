using Granit.Modularity;

namespace Granit.Entities.Views;

/// <summary>
/// Granit module marker for the EntityView abstractions package.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so a consumer module can
/// declare <c>[DependsOn(typeof(GranitEntitiesViewsAbstractionsModule))]</c> and
/// reference the EntityView contracts (<see cref="EntityViewDescriptor"/>,
/// <see cref="EntityViewVisibility"/>, <see cref="EntityViewSharedWith"/>,
/// <see cref="EntityViewPermissions"/>) without taking a runtime dependency on
/// <c>Granit.Entities.Views</c>, the EF Core persistence layer, or the HTTP endpoints.
/// </remarks>
public sealed class GranitEntitiesViewsAbstractionsModule : GranitModule;
