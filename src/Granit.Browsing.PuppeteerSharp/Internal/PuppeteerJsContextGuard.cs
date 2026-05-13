using System.Threading.Tasks;
using IPuppeteerPage = PuppeteerSharp.IPage;

namespace Granit.Browsing.PuppeteerSharp.Internal;

/// <summary>
/// Applies JavaScript-disabled semantics to a freshly-acquired Puppeteer page before any
/// navigation runs, preventing script execution that could occur prior to a follow-up
/// <c>SetJavaScriptEnabledAsync(false)</c>.
/// </summary>
/// <remarks>
/// PuppeteerSharp does not expose a context-level JS-disable knob at launch — the engine
/// reuses a single browser-level renderer pool. The provider therefore calls
/// <c>SetJavaScriptEnabledAsync(false)</c> on the brand-new page object before
/// <c>IPage.GoToAsync(string)</c> is invoked. This is best-effort: a
/// race-free disable would require Playwright's per-context option which Puppeteer lacks.
/// </remarks>
internal static class PuppeteerJsContextGuard
{
    /// <summary>
    /// Disables JavaScript on <paramref name="page"/> when sandbox profile or per-page
    /// options require it. Must be called before any navigation.
    /// </summary>
    public static async Task ApplyAsync(
        IPuppeteerPage page,
        bool pageJsEnabled,
        bool sandboxDisablesJs)
    {
        System.ArgumentNullException.ThrowIfNull(page);

        bool disable = sandboxDisablesJs || !pageJsEnabled;
        if (!disable)
        {
            return;
        }

        await page.SetJavaScriptEnabledAsync(false).ConfigureAwait(false);
    }
}
