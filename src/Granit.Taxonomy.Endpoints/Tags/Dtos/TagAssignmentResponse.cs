namespace Granit.Taxonomy.Endpoints.Tags.Dtos;

/// <summary>Wire-shape response for a <c>TagAssignment</c> row.</summary>
public sealed record TagAssignmentResponse(
    Guid Id,
    Guid? TenantId,
    Guid TagId,
    string TargetType,
    Guid TargetId,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId);
