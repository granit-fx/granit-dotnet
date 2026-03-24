using System.Globalization;
using Granit.MultiTenancy;
using Granit.Templating.GlobalContext;

namespace Granit.Templating.Scriban.GlobalContexts;

/// <summary>
/// Injects ambient request context (culture, tenant, user) into every template
/// under the <c>context</c> namespace.
/// </summary>
/// <remarks>
/// Uses <see cref="ICurrentTenant"/> via <see cref="IServiceProvider"/> so that the
/// module works even when <c>Granit.MultiTenancy</c> is not registered
/// (resolves as <c>NullTenantContext</c> in that case).
/// <para>
/// Available template variables:
/// <list type="table">
///   <listheader><term>Variable</term><description>Example output</description></listheader>
///   <item><term><c>{{ context.culture }}</c></term><description><c>fr-BE</c></description></item>
///   <item><term><c>{{ context.culture_name }}</c></term><description><c>français (Belgique)</c></description></item>
///   <item><term><c>{{ context.tenant_id }}</c></term><description><c>3fa85f64-…</c> or empty string</description></item>
///   <item><term><c>{{ context.tenant_name }}</c></term><description><c>Acme Corp</c> or empty string</description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Security:</strong> user identity is intentionally not exposed here.
/// Avoid injecting PII into template global contexts — pass it through <c>TData</c> instead.
/// </para>
/// </remarks>
internal sealed class ExecutionContextGlobalContext(IServiceProvider serviceProvider) : ITemplateGlobalContext
{

    /// <inheritdoc/>
    public string ContextName => "context";

    /// <inheritdoc/>
    public object Resolve()
    {
        CultureInfo culture = CultureInfo.CurrentCulture;

        // Soft dependency — resolves NullTenantContext when multi-tenancy is not installed
        var tenant = serviceProvider.GetService(typeof(ICurrentTenant)) as ICurrentTenant;

        return new
        {
            culture = culture.Name,
            culture_name = culture.DisplayName,
            tenant_id = tenant?.IsAvailable == true ? tenant.Id?.ToString() ?? string.Empty : string.Empty,
            tenant_name = tenant?.IsAvailable == true ? tenant.Name ?? string.Empty : string.Empty,
        };
    }
}
