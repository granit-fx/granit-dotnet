namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to add an email to a contact.</summary>
public sealed record ContactEmailRequest(string Address, bool IsPrimary = false, string? Label = null);
