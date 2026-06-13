namespace Granit.Identity;

/// <summary>
/// Marker on the no-op <see cref="IUserSessionProvider"/> default. Lets the canonical session endpoints
/// detect at startup that no real backend replaced it and emit a clear warning, rather than silently
/// serving <c>200 []</c> from <c>/sessions</c> and <c>/devices</c>.
/// </summary>
public interface IFallbackUserSessionProvider;
