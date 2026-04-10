using Granit.MultiTenancy;
using Granit.Templating.GlobalContext;
using Microsoft.Extensions.Options;

namespace Granit.Templating.Scriban.GlobalContexts;

/// <summary>
/// Injects application-level metadata into every template under the <c>app</c> namespace.
/// </summary>
/// <remarks>
/// Configured via <see cref="AppGlobalContextOptions"/> (section <c>Granit:Templating:App</c>).
/// <para>
/// When <see cref="ITenantUrlResolver"/> is registered (multi-tenant apps),
/// <c>{{ app.base_url }}</c> resolves to the current tenant's URL.
/// Otherwise, it falls back to the static <see cref="AppGlobalContextOptions.BaseUrl"/>.
/// </para>
/// <para>
/// Available template variables:
/// <list type="table">
///   <listheader><term>Variable</term><description>Example output</description></listheader>
///   <item><term><c>{{ app.name }}</c></term><description><c>Guava Admin</c></description></item>
///   <item><term><c>{{ app.base_url }}</c></term><description><c>https://app.example.com</c> (tenant-aware)</description></item>
///   <item><term><c>{{ app.support_email }}</c></term><description><c>support@example.com</c></description></item>
///   <item><term><c>{{ app.logo_url }}</c></term><description><c>https://cdn.example.com/logo.png</c></description></item>
/// </list>
/// </para>
/// </remarks>
internal sealed class AppGlobalContext(
    IOptions<AppGlobalContextOptions> options,
    IServiceProvider serviceProvider) : ITemplateGlobalContext
{
    /// <inheritdoc/>
    public string ContextName => "app";

    /// <inheritdoc/>
    public object Resolve()
    {
        AppGlobalContextOptions opts = options.Value;

        // Soft dependency: resolve tenant-aware URL when multi-tenancy is configured.
        // ITenantUrlResolver is scoped (async-local ICurrentTenant), safe to resolve here.
        // The call is cached in-memory so sync-over-async hits only the ConcurrentDictionary.
        var urlResolver = serviceProvider.GetService(typeof(ITenantUrlResolver)) as ITenantUrlResolver;
        string baseUrl = urlResolver is not null
            ? urlResolver.ResolveBaseUrlAsync().GetAwaiter().GetResult()
            : opts.BaseUrl;

        return new
        {
            name = opts.Name,
            base_url = baseUrl.TrimEnd('/'),
            support_email = opts.SupportEmail,
            logo_url = opts.LogoUrl,
        };
    }
}
