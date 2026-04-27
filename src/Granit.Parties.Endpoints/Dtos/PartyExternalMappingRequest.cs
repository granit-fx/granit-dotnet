namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to register an external provider identifier on a contact.</summary>
public sealed record PartyExternalMappingRequest(string ProviderName, string ExternalId);
