using Microsoft.AspNetCore.Builder;

namespace Granit.Http.OutputCaching.Extensions;

/// <summary>
/// Extension methods for adding Granit output caching to the ASP.NET Core pipeline.
/// </summary>
public static class OutputCachingApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the output caching middleware to the request pipeline.
    /// </summary>
    /// <remarks>
    /// <b>Middleware ordering is critical.</b> This must be called:
    /// <list type="bullet">
    ///   <item><b>After</b> <c>UseRouting()</c></item>
    ///   <item><b>After</b> <c>UseCors()</c> (ASP.NET Core requirement)</item>
    ///   <item><b>Before</b> <c>UseAuthorization()</c> if caching anonymous-only responses</item>
    /// </list>
    /// <code>
    /// app.UseRouting();
    /// app.UseCors();
    /// app.UseGranitOutputCaching();  // ← here
    /// app.UseAuthorization();
    /// </code>
    /// </remarks>
    public static IApplicationBuilder UseGranitOutputCaching(this IApplicationBuilder app) =>
        app.UseOutputCache();
}
