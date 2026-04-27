namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to suspend a contact.</summary>
public sealed record PartySuspendRequest(string? Reason = null);
