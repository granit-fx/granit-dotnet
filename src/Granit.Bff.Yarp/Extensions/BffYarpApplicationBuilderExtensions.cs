using Microsoft.AspNetCore.Builder;

namespace Granit.Bff.Yarp.Extensions;

/// <summary>
/// Extension methods for adding BFF YARP middleware to the request pipeline.
/// </summary>
public static class BffYarpApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the YARP reverse proxy middleware. Must be called after <c>UseAuthentication()</c>.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application for further chaining.</returns>
    public static WebApplication UseGranitBffYarp(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapReverseProxy();

        return app;
    }
}
