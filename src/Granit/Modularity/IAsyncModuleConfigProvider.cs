namespace Granit.Modularity;

/// <summary>
/// Asynchronous variant of <see cref="IModuleConfigProvider{TResponse}"/> for modules whose
/// configuration requires async resolution (e.g., runtime settings via <c>ISettingProvider</c>).
/// </summary>
/// <typeparam name="TResponse">
/// The response DTO type exposed to clients.
/// Must be a <c>sealed record</c> following the <c>*Response</c> naming convention.
/// </typeparam>
/// <remarks>
/// <para>
/// Use this interface instead of <see cref="IModuleConfigProvider{TResponse}"/> when the
/// configuration values are resolved asynchronously (database-backed settings, external services).
/// </para>
/// <para>
/// For HTTP exposure, use
/// <c>MapGranitModuleConfigAsync&lt;TProvider, TResponse&gt;()</c> from <c>Granit.Endpoints</c>.
/// </para>
/// <example>
/// <code>
/// internal sealed class IdentityLocalConfigProvider(ISettingProvider settings)
///     : IAsyncModuleConfigProvider&lt;IdentityLocalConfigResponse&gt;
/// {
///     public async Task&lt;IdentityLocalConfigResponse&gt; GetConfigAsync(CancellationToken cancellationToken)
///     {
///         string? value = await settings.GetOrNullAsync("Identity.Local.AllowSelfRegistration", cancellationToken);
///         return new(string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
///     }
/// }
/// </code>
/// </example>
/// </remarks>
public interface IAsyncModuleConfigProvider<TResponse> where TResponse : class
{
    /// <summary>
    /// Returns the current module configuration as a public-facing DTO.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<TResponse> GetConfigAsync(CancellationToken cancellationToken = default);
}
