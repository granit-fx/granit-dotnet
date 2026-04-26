namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to suspend a contact.</summary>
public sealed record ContactSuspendRequest(string? Reason = null);
