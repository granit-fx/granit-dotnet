namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to suspend a party.</summary>
public sealed record PartySuspendRequest(string? Reason = null);
