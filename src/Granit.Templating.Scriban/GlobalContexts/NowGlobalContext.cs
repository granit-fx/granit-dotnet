using Granit.Templating.GlobalContext;
using Granit.Timing;

namespace Granit.Templating.Scriban.GlobalContexts;

/// <summary>
/// Injects the current date and time into every template under the <c>now</c> namespace.
/// </summary>
/// <remarks>
/// Uses <see cref="IClock"/> (testable, timezone-aware) rather than <c>DateTime.Now</c>.
/// Registered as a singleton by <see cref="GranitTemplatingScribanModule"/>.
/// <para>
/// Available template variables:
/// <list type="table">
///   <listheader><term>Variable</term><description>Example output</description></listheader>
///   <item><term><c>{{ now.date }}</c></term><description><c>27/02/2026</c></description></item>
///   <item><term><c>{{ now.datetime }}</c></term><description><c>27/02/2026 14:35</c></description></item>
///   <item><term><c>{{ now.iso }}</c></term><description><c>2026-02-27T14:35:00+00:00</c></description></item>
///   <item><term><c>{{ now.year }}</c></term><description><c>2026</c></description></item>
///   <item><term><c>{{ now.month }}</c></term><description><c>02</c></description></item>
///   <item><term><c>{{ now.time }}</c></term><description><c>14:35</c></description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class NowGlobalContext(IClock clock) : ITemplateGlobalContext
{
    private readonly IClock _clock = clock;

    /// <inheritdoc/>
    public string ContextName => "now";

    /// <inheritdoc/>
    public Task<object> ResolveAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _clock.Now;
        return Task.FromResult<object>(new
        {
            date = now.ToString("dd/MM/yyyy"),
            datetime = now.ToString("dd/MM/yyyy HH:mm"),
            iso = now.ToString("O"),
            year = now.Year.ToString("D4"),
            month = now.Month.ToString("D2"),
            day = now.Day.ToString("D2"),
            time = now.ToString("HH:mm"),
        });
    }
}
