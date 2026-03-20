using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Endpoints;
using Granit.ReferenceData.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.ReferenceData.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering reference data endpoints.
/// </summary>
public static class ReferenceDataEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps reference data CRUD endpoints for <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="ReferenceDataEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// <para>
    /// Registers read endpoints (GET) accessible to all authenticated users and
    /// admin endpoints (POST, PUT, DELETE) protected by a configurable authorization policy.
    /// </para>
    /// <para>
    /// The entity type name is converted to a kebab-case segment appended to the route prefix.
    /// For example, <c>MapReferenceDataEndpoints&lt;Country&gt;()</c> creates routes under
    /// <c>/reference-data/country</c>.
    /// </para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapReferenceDataEndpoints&lt;Country&gt;();
    /// app.MapReferenceDataEndpoints&lt;Currency&gt;(opts => opts.AdminPolicyName = "Custom.Policy");
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapReferenceDataEndpoints<TEntity>(
        this IEndpointRouteBuilder endpoints,
        Action<ReferenceDataEndpointsOptions>? configure = null)
        where TEntity : ReferenceDataEntity, new()
    {
        ReferenceDataEndpointsOptions options = new();
        configure?.Invoke(options);

        // Register the admin authorization policy (role-based fallback)
        if (options.AdminPolicyName is not null)
        {
            IOptions<AuthorizationOptions> authOptions =
                endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
            authOptions.Value.AddPolicy(
                options.AdminPolicyName,
                policy => policy.RequireRole(options.RequiredRole));
        }

        string entitySegment = ToKebabCase(typeof(TEntity).Name);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup($"{options.RoutePrefix}/{entitySegment}")
            .WithTags(options.TagName);

        group.MapReadEndpoints<TEntity>();
        group.MapAdminEndpoints<TEntity>(options.AdminPolicyName);

        return group;
    }

    private static string ToKebabCase(string typeName)
    {
        System.Text.StringBuilder sb = new();
        for (int i = 0; i < typeName.Length; i++)
        {
            char c = typeName[i];
            if (char.IsUpper(c) && i > 0)
            {
                sb.Append('-');
            }
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}
