namespace Granit.Metering.Endpoints.Dtos;

/// <summary>HTTP body for <c>POST /metering/events/{id}/deprecate</c>.</summary>
/// <param name="Reason">Free-text justification (max 500 chars). Stored on the event for audit and surfaced in the audit log.</param>
public sealed record DeprecateEventRequest(string Reason);
