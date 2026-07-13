using Granit.MultiTenancy;
using Granit.Templating.GlobalContext;
using Microsoft.Extensions.DependencyInjection;
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
    IServiceScopeFactory scopeFactory) : ITemplateGlobalContext
{
    /// <inheritdoc/>
    public string ContextName => "app";

    /// <inheritdoc/>
    public async Task<object> ResolveAsync(CancellationToken cancellationToken = default)
    {
        AppGlobalContextOptions opts = options.Value;

        // ITenantUrlResolver is scoped (depends on ITenantReader/EF Core).
        // AppGlobalContext is a singleton, so we create a short-lived scope to resolve it.
        // The resolver uses an in-memory cache so the scope + DB overhead is minimal.
        string? resolvedUrl;
        await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
        {
            ITenantUrlResolver? urlResolver = scope.ServiceProvider.GetService<ITenantUrlResolver>();
            resolvedUrl = urlResolver is null
                ? null
                : await urlResolver.ResolveBaseUrlAsync(cancellationToken).ConfigureAwait(false);
        }

        string baseUrl = !string.IsNullOrEmpty(resolvedUrl) ? resolvedUrl : opts.BaseUrl;

        return new
        {
            name = opts.Name,
            base_url = baseUrl.TrimEnd('/'),
            support_email = opts.SupportEmail,
            logo_url = opts.LogoUrl,
        };
    }
}
