using Granit.Authentication.JwtBearer.BackChannelLogout;
using Granit.Authentication.JwtBearer.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.Extensions;

/// <summary>
/// Extensions for mapping OIDC back-channel logout endpoints on <see cref="IEndpointRouteBuilder"/>.
/// </summary>
public static class JwtBearerEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the OIDC back-channel logout endpoint when
    /// <see cref="BackChannelLogoutOptions.Enabled"/> is <c>true</c>.
    /// The endpoint accepts anonymous POST requests (the identity provider calls it server-to-server).
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The <paramref name="endpoints"/> for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitBackChannelLogout(this IEndpointRouteBuilder endpoints)
    {
        JwtBearerAuthOptions options = endpoints.ServiceProvider
            .GetRequiredService<IOptions<JwtBearerAuthOptions>>().Value;

        if (!options.BackChannelLogout.Enabled)
        {
            return endpoints;
        }

        endpoints
            .MapPost(options.BackChannelLogout.EndpointPath, BackChannelLogoutEndpoint.HandleAsync)
            .AddEndpointFilter<FormContentTypeEndpointFilter>()
            .AllowAnonymous()
            .ExcludeFromDescription();

        return endpoints;
    }

}
