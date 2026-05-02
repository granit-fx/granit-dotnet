using Granit.Entities.Customization.Domain.Deltas;

namespace Granit.Entities.Customization.Endpoints.Dtos;

/// <summary>
/// PUT payload for the entities-customization endpoint. Replaces the entire
/// delta list for a single (tenant, entity, layout) — partial / append updates
/// are NOT supported (ADR-053 §Storage shape: full replace).
/// </summary>
/// <param name="Deltas">
/// Ordered closed-vocabulary delta list. Polymorphism is driven by the
/// <c>$type</c> discriminator declared on <see cref="LayoutDelta"/>
/// (<c>reorder</c> / <c>regroup</c> / <c>hide</c>).
/// </param>
public sealed record EntityCustomizationRequest(IReadOnlyList<LayoutDelta> Deltas);
