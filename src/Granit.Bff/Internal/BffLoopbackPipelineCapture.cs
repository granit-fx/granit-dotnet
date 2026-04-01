using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Granit.Bff.Internal;

/// <summary>
/// Captures the ASP.NET Core request pipeline at startup by registering a transparent
/// middleware as the first component in the chain. The captured <see cref="Pipeline"/>
/// is used by <see cref="InternalLoopbackHandler"/> to invoke the pipeline in-memory,
/// avoiding TCP loopback deadlocks when BFF and OpenIddict run in the same process.
/// </summary>
internal sealed class BffLoopbackPipelineCapture : IStartupFilter
{
    /// <summary>
    /// The full ASP.NET Core middleware pipeline, captured after startup completes.
    /// <c>null</c> until the host finishes building the pipeline.
    /// </summary>
    internal RequestDelegate? Pipeline { get; set; }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(pipeline =>
            {
                Pipeline = pipeline;
                return pipeline;
            });
            next(app);
        };
}
