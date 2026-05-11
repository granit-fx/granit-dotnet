using System;
using Microsoft.Extensions.FileSystemGlobbing;

namespace Granit.Browsing.Pages;

/// <summary>
/// Provider-neutral URL match pattern based on
/// <see cref="Microsoft.Extensions.FileSystemGlobbing.Matcher"/>. Used by
/// <see cref="IBrowserPage.RouteAsync(RoutePattern, System.Func{RouteRequest, System.Threading.CancellationToken, System.Threading.Tasks.ValueTask{RouteDecision}}, System.Threading.CancellationToken)"/>
/// to express which requests a handler should observe.
/// </summary>
/// <remarks>
/// <para>
/// The glob is normalised to <c>host/path</c> form before matching: <c>**/*</c> matches
/// everything, <c>api.example.com/**</c> matches every request on that host, and
/// <c>**.example.com/**</c> matches subdomains.
/// </para>
/// <para>
/// Host comparison is case-insensitive (DNS is case-insensitive); path comparison is
/// case-sensitive (paths are not).
/// </para>
/// </remarks>
/// <param name="Glob">The raw glob pattern.</param>
public sealed record RoutePattern(string Glob)
{
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

        string candidate = $"{url.Host.ToLowerInvariant()}{url.AbsolutePath}";
        string pattern = NormaliseHostSegment(Glob);

        Matcher matcher = new();
        matcher.AddInclude(pattern);
        return matcher.Match(candidate).HasMatches;
    }

    /// <inheritdoc/>
    public override string ToString() => Glob;

    private static string NormaliseHostSegment(string glob)
    {
        // Globbing matcher operates on path-like segments. Lower the host portion to keep
        // host matching case-insensitive without forcing callers to lower their patterns.
        int slash = glob.IndexOf('/');
        return slash < 0
            ? glob.ToLowerInvariant()
            : glob[..slash].ToLowerInvariant() + glob[slash..];
    }
}
