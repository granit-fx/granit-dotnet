using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;

namespace Granit.Entities.Customization.Endpoints.Dtos;

/// <summary>
/// GET / PUT response shape for an entity customization. Mirrors the persisted
/// <see cref="EntityCustomization"/> aggregate with the wire-friendly enum
/// rendering and the polymorphic delta payload.
/// </summary>
public sealed record EntityCustomizationResponse(
    Guid Id,
    string EntityName,
    LayoutKind LayoutKind,
    IReadOnlyList<LayoutDelta> Deltas);
