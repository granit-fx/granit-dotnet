namespace Granit.Modularity;

/// <summary>
/// Provides a read-only snapshot of a module's configuration for client consumption.
/// </summary>
/// <typeparam name="TResponse">
/// The response DTO type exposed to clients.
/// Must be a <c>sealed record</c> following the <c>*Response</c> naming convention.
/// </typeparam>
/// <remarks>
/// <para>
/// Implement this interface in each module that needs to expose its <c>IOptions&lt;T&gt;</c>
/// configuration to external consumers. The implementation maps internal options to a
/// public-facing DTO — never expose the raw options class.
/// </para>
/// <para>
/// For HTTP exposure, use <c>MapGranitModuleConfig&lt;TProvider, TResponse&gt;()</c>
/// from <c>Granit.Endpoints</c> to map a standardized <c>GET /{module}/config</c> endpoint.
/// </para>
/// <example>
/// <code>
/// internal sealed class WebhookModuleConfigProvider(IOptions&lt;WebhooksOptions&gt; options)
///     : IModuleConfigProvider&lt;WebhookModuleConfigResponse&gt;
/// {
///     public WebhookModuleConfigResponse GetConfig() =&gt;
///         new(options.Value.StorePayload);
/// }
/// </code>
/// </example>
/// </remarks>
public interface IModuleConfigProvider<out TResponse> where TResponse : class
{
    /// <summary>
    /// Returns the current module configuration as a public-facing DTO.
    /// </summary>
    TResponse GetConfig();
}
