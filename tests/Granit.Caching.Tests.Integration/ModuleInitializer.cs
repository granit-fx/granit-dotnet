using System.Runtime.CompilerServices;

namespace Granit.Caching.Tests.Integration;

/// <summary>
/// Raises the process-wide default <see cref="System.Text.RegularExpressions.Regex"/> match timeout
/// before <c>Testcontainers</c> initializes its image-parsing regexes.
/// </summary>
/// <remarks>
/// Testcontainers' <c>MatchImage</c> uses default-timeout regexes that can hit
/// <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/> on CI runners under load
/// (the AppDomain-wide default in .NET is 10 s only when explicitly set; if a tight value has been
/// inherited from the test host, parsing a perfectly valid image string like <c>"redis:7-alpine"</c>
/// can still time out). Pinning the default to <see cref="Timeout.InfiniteTimeSpan"/> for this
/// integration assembly mirrors the upstream guidance and removes the flake without altering
/// production behaviour (this assembly only runs Testcontainers-backed integration tests).
/// </remarks>
internal static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        AppContext.SetData("REGEX_DEFAULT_MATCH_TIMEOUT", Timeout.InfiniteTimeSpan);
    }
}
