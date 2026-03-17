using Microsoft.AspNetCore.Builder;

namespace Granit.Http.ExceptionHandling.Extensions;

/// <summary>
/// Extensions for configuring the Granit exception handling middleware.
/// </summary>
public static class ExceptionHandlingApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the ASP.NET Core exception handler middleware to the pipeline.
    /// </summary>
    /// <remarks>
    /// <b>Must be called before all other middleware</b> (routing, authentication,
    /// authorization) to ensure exceptions from the entire pipeline are caught.
    /// </remarks>
    /// <example>
    /// <code>
    /// app.UseGranitExceptionHandling(); // First
    /// app.UseRouting();
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// app.MapControllers();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseGranitExceptionHandling(
        this IApplicationBuilder app) =>
        app.UseExceptionHandler();
}
