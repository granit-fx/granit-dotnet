using Granit.Http.Hosting.Cors.Extensions;
using Granit.Http.Hosting.ResponseCompression.Extensions;
using Granit.Modularity;

namespace Granit.Http.Hosting;

/// <summary>
/// Granit module consolidating the thin host-facing HTTP pipeline wrappers:
/// standardized CORS (ISO 27001 wildcard rules, auto-applied middleware) and
/// response compression (Brotli + gzip, BREACH-aware, SSE-safe defaults).
/// </summary>
/// <remarks>
/// <para>
/// Both features keep their historical configuration sections — <c>Http:Cors</c> and
/// <c>Http:ResponseCompression</c> — so host configuration survives the package fold
/// (audit consolidation, story #3000). The sections deliberately do not carry a
/// <c>Hosting</c> segment: they name the FEATURE, and the hosting package is an
/// implementation detail that may absorb more wrappers later.
/// </para>
/// </remarks>
public sealed class GranitHttpHostingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Builder.AddGranitCors();
        context.Builder.AddGranitResponseCompression();
    }
}
