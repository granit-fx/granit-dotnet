namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>
/// Wire-shape request for <c>POST /api/v1/taxonomy/tags/{id}/assign</c>.
/// </summary>
/// <param name="TargetType">Polymorphic discriminator (assembly-qualified type name of the target aggregate).</param>
/// <param name="TargetId">Identifier of the target aggregate row.</param>
public sealed record AssignTagRequest(string TargetType, Guid TargetId);
