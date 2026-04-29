using Granit.Analytics;
using Granit.Analytics.Endpoints.Dtos;
using Granit.Timing;

namespace Granit.Analytics.Endpoints.Internal;

/// <summary>
/// Resolves a <see cref="PeriodSpec"/> (with optional named token) into an absolute
/// <c>[from, to)</c> window using <see cref="IClock"/> as the time source. All
/// resolutions are deterministic and never call <c>DateTimeOffset.UtcNow</c> directly
/// (per Granit framework conventions).
/// </summary>
internal sealed class PeriodResolver(IClock clock)
{
    private readonly IClock _clock = clock;

    /// <summary>
    /// Resolves the main period to absolute bounds.
    /// </summary>
    public ResolvedPeriod Resolve(PeriodSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (!string.IsNullOrWhiteSpace(spec.Token))
        {
            return ResolveToken(spec.Token, _clock.Now);
        }

        if (spec.From is null || spec.To is null)
        {
            throw new ArgumentException("PeriodSpec requires either Token or both From and To.", nameof(spec));
        }

        if (spec.From >= spec.To)
        {
            throw new ArgumentException("PeriodSpec.From must be strictly before PeriodSpec.To.", nameof(spec));
        }

        return new ResolvedPeriod(spec.From.Value, spec.To.Value);
    }

    /// <summary>
    /// Resolves the comparison window. Supports the special token <c>"previous_period"</c>
    /// which returns the equal-length window immediately preceding <paramref name="main"/>.
    /// </summary>
    public ResolvedPeriod ResolveComparison(PeriodSpec spec, ResolvedPeriod main)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (string.Equals(spec.Token, "previous_period", StringComparison.OrdinalIgnoreCase))
        {
            TimeSpan length = main.To - main.From;
            return new ResolvedPeriod(main.From - length, main.From);
        }

        return Resolve(spec);
    }

    private static ResolvedPeriod ResolveToken(string token, DateTimeOffset now)
    {
        DateTimeOffset todayStart = new(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset);

        return token.ToLowerInvariant() switch
        {
            "today" => new(todayStart, todayStart.AddDays(1)),
            "yesterday" => new(todayStart.AddDays(-1), todayStart),
            "last_60s" => new(now.AddSeconds(-60), now),
            "last_5m" => new(now.AddMinutes(-5), now),
            "last_7d" => new(todayStart.AddDays(-7), todayStart.AddDays(1)),
            "last_30d" => new(todayStart.AddDays(-30), todayStart.AddDays(1)),
            "mtd" => new(new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset), todayStart.AddDays(1)),
            "qtd" => ResolveQuarterToDate(now, todayStart),
            "ytd" => new(new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset), todayStart.AddDays(1)),
            _ => throw new ArgumentException(
                $"Unknown period token '{token}'. Supported: today, yesterday, last_60s, last_5m, last_7d, last_30d, mtd, qtd, ytd."),
        };
    }

    private static ResolvedPeriod ResolveQuarterToDate(DateTimeOffset now, DateTimeOffset todayStart)
    {
        int quarterStartMonth = ((now.Month - 1) / 3 * 3) + 1;
        DateTimeOffset start = new(now.Year, quarterStartMonth, 1, 0, 0, 0, now.Offset);
        return new(start, todayStart.AddDays(1));
    }
}

