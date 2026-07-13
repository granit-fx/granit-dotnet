namespace Granit.Templating.GlobalContext;

/// <summary>
/// Provides an ambient context object injected into every template under a named variable.
/// </summary>
/// <remarks>
/// Implementations are registered as singletons and resolved by <see cref="Pipeline.ITemplateEngine"/>
/// before each render. Each context is exposed in templates under <see cref="ContextName"/>.
/// <para>
/// Built-in implementations (in <c>Granit.Templating.Scriban</c>):
/// <list type="table">
///   <listheader><term>ContextName</term><description>Variables</description></listheader>
///   <item>
///     <term><c>now</c></term>
///     <description><c>{{ now.date }}</c>, <c>{{ now.datetime }}</c>, <c>{{ now.iso }}</c>, <c>{{ now.year }}</c>, <c>{{ now.time }}</c></description>
///   </item>
///   <item>
///     <term><c>context</c></term>
///     <description><c>{{ context.culture }}</c>, <c>{{ context.tenant_id }}</c>, <c>{{ context.user }}</c></description>
///   </item>
/// </list>
/// </para>
/// <para>
/// To add custom ambient variables (e.g. branding):
/// <code>
/// public sealed class AcmeBrandingContext : ITemplateGlobalContext
/// {
///     public string ContextName => "brand";
///     public Task&lt;object&gt; ResolveAsync(CancellationToken cancellationToken = default) =>
///         Task.FromResult&lt;object&gt;(new { logo_url = "https://...", primary_color = "#..." });
/// }
///
/// services.AddSingleton&lt;ITemplateGlobalContext, AcmeBrandingContext&gt;();
/// </code>
/// </para>
/// </remarks>
public interface ITemplateGlobalContext
{
    /// <summary>
    /// The template variable name under which the resolved object is exposed.
    /// Must be a valid Scriban identifier (lowercase, no spaces).
    /// Example: <c>"now"</c>, <c>"context"</c>, <c>"brand"</c>.
    /// </summary>
    string ContextName { get; }

    /// <summary>
    /// Resolves and returns the context object for the current request.
    /// Called once per render — implementations should be lightweight. Async-first so
    /// implementations backed by I/O (settings store, tenant URL resolution) never have
    /// to block a thread with sync-over-async.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An object whose public properties are accessible as template variables
    /// under <see cref="ContextName"/>. Must not expose PII or secrets.
    /// </returns>
    Task<object> ResolveAsync(CancellationToken cancellationToken = default);
}
