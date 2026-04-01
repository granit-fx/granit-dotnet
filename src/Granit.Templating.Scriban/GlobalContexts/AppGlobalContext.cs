using Granit.Templating.GlobalContext;
using Microsoft.Extensions.Options;

namespace Granit.Templating.Scriban.GlobalContexts;

/// <summary>
/// Injects application-level metadata into every template under the <c>app</c> namespace.
/// </summary>
/// <remarks>
/// Configured via <see cref="AppGlobalContextOptions"/> (section <c>Granit:Templating:App</c>).
/// <para>
/// Available template variables:
/// <list type="table">
///   <listheader><term>Variable</term><description>Example output</description></listheader>
///   <item><term><c>{{ app.name }}</c></term><description><c>Guava Admin</c></description></item>
///   <item><term><c>{{ app.base_url }}</c></term><description><c>https://app.example.com</c></description></item>
///   <item><term><c>{{ app.support_email }}</c></term><description><c>support@example.com</c></description></item>
///   <item><term><c>{{ app.logo_url }}</c></term><description><c>https://cdn.example.com/logo.png</c></description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class AppGlobalContext(IOptions<AppGlobalContextOptions> options) : ITemplateGlobalContext
{
    /// <inheritdoc/>
    public string ContextName => "app";

    /// <inheritdoc/>
    public object Resolve()
    {
        AppGlobalContextOptions opts = options.Value;
        return new
        {
            name = opts.Name,
            base_url = opts.BaseUrl.TrimEnd('/'),
            support_email = opts.SupportEmail,
            logo_url = opts.LogoUrl,
        };
    }
}
