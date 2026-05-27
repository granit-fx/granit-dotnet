using Granit.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Features.AspNetCore;

/// <summary>
/// Minimal API endpoint filter that enforces a feature check before the endpoint handler runs.
/// Registered via <see cref="FeatureEndpointConventionBuilderExtensions.RequiresFeature"/>.
/// </summary>
internal sealed class RequiresFeatureEndpointFilter(string featureName) : IEndpointFilter
{
    private readonly string _featureName = featureName;

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        IFeatureChecker checker =
            context.HttpContext.RequestServices.GetRequiredService<IFeatureChecker>();

        await checker.RequireEnabledAsync(_featureName, context.HttpContext.RequestAborted).ConfigureAwait(false);

        return await next(context).ConfigureAwait(false);
    }
}
