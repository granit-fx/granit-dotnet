using Granit.Modularity;

namespace Granit.Entities.Customization;

/// <summary>
/// Module marker for <c>Granit.Entities.Customization</c> — the Layer 1
/// tenant-customization runtime per ADR-053. Pull this from hosts that
/// resolve and persist customization deltas; the manifest composer (B4)
/// consumes the reader to apply deltas at request time.
/// </summary>
[DependsOn(typeof(GranitEntitiesAbstractionsModule))]
public sealed class GranitEntitiesCustomizationModule : GranitModule;
