using Microsoft.AspNetCore.Authorization;

namespace Granit.Authorization.Authorization;

/// <summary>
/// Authorization requirement satisfied when the current user owns the
/// <see cref="Granit.Domain.IOwnable"/> resource being authorized.
/// </summary>
/// <remarks>
/// Pair with a resource-based authorization call:
/// <c>authorizationService.AuthorizeAsync(user, resource, new OwnedRequirement())</c>.
/// The matching handler is <c>OwnedByCurrentUserHandler</c>.
/// </remarks>
public sealed class OwnedRequirement : IAuthorizationRequirement;
