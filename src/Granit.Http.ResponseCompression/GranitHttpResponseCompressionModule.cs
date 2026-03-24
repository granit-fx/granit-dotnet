using Granit.Http.ResponseCompression.Extensions;
using Granit.Modularity;

namespace Granit.Http.ResponseCompression;

/// <summary>
/// Granit module for standardized HTTP response compression.
/// </summary>
/// <remarks>
/// Registers Brotli + gzip response compression with safe defaults via
/// <see cref="ResponseCompressionHostApplicationBuilderExtensions.AddGranitResponseCompression"/>.
/// HTTPS compression is enabled (BREACH mitigated by antiforgery enforcement).
/// SSE streams (<c>text/event-stream</c>) are always excluded.
/// </remarks>
public sealed class GranitHttpResponseCompressionModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitResponseCompression();
}
