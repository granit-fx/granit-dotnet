using Granit.Workspaces.Endpoints.Landing;

namespace Granit.Workspaces.Endpoints.Dtos;

/// <summary>Response payload for <c>GET /api/me/landing-route</c>.</summary>
/// <param name="Route">The resolved landing route (e.g. <c>"/w/Granit.Framework"</c>).</param>
/// <param name="Source">Which tier of the precedence chain produced the route.</param>
public sealed record LandingRouteResponse(string Route, LandingRouteSource Source);

/// <summary>Request body for <c>PUT /api/me/landing-route/pinned</c>.</summary>
/// <param name="Route">The route to pin, or <see langword="null"/> to clear the pin.</param>
public sealed record SetPinnedLandingRouteRequest(string? Route);
