namespace Granit.Identity;

/// <summary>
/// A user session enriched for presentation: the canonical <see cref="UserSessionDescriptor"/>
/// (with geolocation resolved) paired with its persisted risk verdict, when one exists.
/// </summary>
/// <remarks>
/// This is what <see cref="IUserSessionManager"/> returns — the descriptor comes from the backend
/// <see cref="IUserSessionProvider"/>, the manager fills <see cref="UserSessionDescriptor.Location"/>
/// and attaches <see cref="Risk"/> read from <see cref="IIdentitySecurityStateStore"/>. Keeping risk as a
/// separate member (rather than folding it into the descriptor) preserves the descriptor's role as the
/// detector's input and avoids a circular shape.
/// </remarks>
/// <param name="Session">The enriched session descriptor (geolocation resolved).</param>
/// <param name="Risk">The persisted risk verdict, or <see langword="null"/> when none was recorded.</param>
public sealed record UserSessionView(
    UserSessionDescriptor Session,
    UserSessionRiskVerdict? Risk);
