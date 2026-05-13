using System;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Granit.Browsing.Pages;

/// <summary>
/// Provider-neutral URL match pattern that combines an anchored host matcher with a
/// path glob (<see cref="Microsoft.Extensions.FileSystemGlobbing.Matcher"/>). Used by
/// <see cref="IBrowserPage.RouteAsync(RoutePattern, System.Func{RouteRequest, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask{RouteDecision}}, System.Threading.CancellationToken)"/>
/// to express which requests a handler should observe.
/// </summary>
/// <remarks>
/// <para>
/// The glob is split on the first <c>/</c> into a host pattern and a path glob. The host
/// pattern is matched against <c>Uri.Host</c> exactly (case-insensitive) — wildcards are
/// restricted to leading <c>*.</c> (subdomain-or-apex) and <c>**.</c> (strict subdomain)
/// forms; mid-host wildcards are NOT supported. The path glob is then applied to
/// <c>Uri.AbsolutePath</c> through the standard <see cref="Matcher"/>. This anchored
/// split prevents the host-confusion class of attacks where <c>evil.com/api.partner.com/exfil</c>
/// could otherwise match a pattern like <c>api.partner.com/**</c>.
/// </para>
/// <para>
/// Supported host patterns:
/// </para>
/// <list type="bullet">
///   <item><c>*</c> alone — matches every host.</item>
///   <item><c>**</c> — matches every host (path-segment-style wildcard at host level).</item>
///   <item><c>*.example.com</c> — matches <c>example.com</c> and any subdomain.</item>
///   <item><c>**.example.com</c> — matches any strict subdomain (<c>example.com</c> excluded).</item>
///   <item><c>api.example.com</c> — exact, case-insensitive match.</item>
/// </list>
/// <para>
/// Host comparison is case-insensitive; path comparison is case-sensitive.
/// </para>
/// </remarks>
/// <param name="Glob">The raw glob pattern.</param>
public sealed record RoutePattern(string Glob)
{
    // ConditionalWeakTable backed by the Glob string keeps the compiled form alive only
    // as long as patterns are referenced, avoiding both per-call allocation and
    // record-equality leakage (instance fields participate in record equality, but a
    // static cache keyed on Glob does not).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Compiled> CompiledCache = new();

    /// <summary>Parses <paramref name="glob"/> with basic validation.</summary>
    /// <exception cref="ArgumentException">When <paramref name="glob"/> is empty or whitespace.</exception>
    public static RoutePattern Parse(string glob)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(glob);

        // Reject control characters: \r \n \t and ASCII < 0x20 leak through globbing.
        foreach (char c in glob)
        {
            if (c < 0x20)
            {
                throw new ArgumentException(
                    $"Route pattern contains a control character (0x{(int)c:X2}).",
                    nameof(glob));
            }
        }

        return new RoutePattern(glob);
    }

    /// <summary>Returns <c>true</c> when <paramref name="url"/> matches the pattern.</summary>
    public bool IsMatch(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        Compiled c = CompiledCache.GetOrAdd(Glob, Compile);
        return c.MatchesHost(url.Host) && c.MatchesPath(url.AbsolutePath);
    }

    /// <inheritdoc/>
    public override string ToString() => Glob;

    private static Compiled Compile(string glob)
    {
        // Split on the first '/' into host + path. When the pattern is host-only (no
        // slash), assume "match any path" — equivalent to "{host}/**".
        int slash = glob.IndexOf('/');
        string hostPattern = slash < 0 ? glob : glob[..slash];
        string pathGlob = slash < 0 ? "/**" : glob[slash..];

        Matcher pathMatcher = new();
        // Matcher path patterns must not start with a leading '/'; strip it.
        string normalisedPath = pathGlob.StartsWith('/') ? pathGlob[1..] : pathGlob;
        if (string.IsNullOrEmpty(normalisedPath))
        {
            normalisedPath = "**";
        }
        pathMatcher.AddInclude(normalisedPath);

        return new Compiled(hostPattern.ToLowerInvariant(), pathMatcher);
    }

    private sealed class Compiled(string hostPatternLower, Matcher pathMatcher)
    {
        public bool MatchesHost(string host)
        {
            string h = host.ToLowerInvariant();

            // Wildcards that match every host.
            if (hostPatternLower is "*" or "**")
            {
                return true;
            }

            // Strict-subdomain wildcard "**.suffix" — host must end with ".suffix"
            // and have at least one character before the dot.
            if (hostPatternLower.StartsWith("**.", StringComparison.Ordinal))
            {
                string suffix = hostPatternLower[3..];
                if (suffix.Length == 0)
                {
                    return false;
                }
                return h.Length > suffix.Length + 1
                    && h.EndsWith("." + suffix, StringComparison.Ordinal);
            }

            // Subdomain-or-apex wildcard "*.suffix" — matches "suffix" OR "*.suffix".
            if (hostPatternLower.StartsWith("*.", StringComparison.Ordinal))
            {
                string suffix = hostPatternLower[2..];
                if (suffix.Length == 0)
                {
                    return false;
                }
                if (h == suffix)
                {
                    return true;
                }
                return h.Length > suffix.Length + 1
                    && h.EndsWith("." + suffix, StringComparison.Ordinal);
            }

            // No wildcards allowed inside the host — exact match only.
            return h == hostPatternLower;
        }

        public bool MatchesPath(string absolutePath)
        {
            string path = absolutePath.StartsWith('/') ? absolutePath[1..] : absolutePath;
            if (path.Length == 0)
            {
                // FileSystemGlobbing.Matcher does not match an empty input even against
                // `**` or `*`. Substitute a sentinel single-segment "index" so empty
                // paths (the URL root "/") behave like a regular path component.
                path = "index";
            }
            return pathMatcher.Match(path).HasMatches;
        }
    }
}
