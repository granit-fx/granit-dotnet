using Granit.Contacts.Domain;

namespace Granit.Contacts.Endpoints.Dtos;

/// <summary>Request to add or remove a role flag on a contact.</summary>
public sealed record ContactRoleRequest(ContactRoles Role);
