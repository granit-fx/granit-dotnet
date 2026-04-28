using Granit.Parties.Domain;

namespace Granit.Parties.Endpoints.Dtos;

/// <summary>Request to add or remove a role flag on a party.</summary>
public sealed record PartyRoleRequest(PartyRoles Role);
