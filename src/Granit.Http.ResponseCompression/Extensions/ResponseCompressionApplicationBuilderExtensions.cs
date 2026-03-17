using Microsoft.AspNetCore.Builder;

namespace Granit.Http.ResponseCompression.Extensions;

/// <summary>
/// Extension methods for adding Granit response compression to the middleware pipeline.
/// </summary>
public static class ResponseCompressionApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the ASP.NET Core response compression middleware to the pipeline.
    /// </summary>
    /// <remarks>
    /// <b>Must be called before any middleware that produces response bodies</b>
    /// (routing, output cache, authorization, endpoints).
    /// <example>
    /// <code>
    /// app.UseGranitResponseCompression(); // Early in pipeline
    /// app.UseOutputCache();
    /// app.UseAuthorization();
    /// app.MapControllers();
    /// </code>
    /// </example>
    /// </remarks>
    public static IApplicationBuilder UseGranitResponseCompression(
        this IApplicationBuilder app) =>
        app.UseResponseCompression();
}
