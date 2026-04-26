namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to register an external provider identifier on a contact.</summary>
public sealed record ContactExternalMappingRequest(string ProviderName, string ExternalId);
