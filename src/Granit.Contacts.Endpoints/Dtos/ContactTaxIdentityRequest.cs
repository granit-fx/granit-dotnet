namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to update a contact's tax / legal identity.</summary>
public sealed record ContactTaxIdentityRequest(string? TaxId, string? RegistrationNumber);
