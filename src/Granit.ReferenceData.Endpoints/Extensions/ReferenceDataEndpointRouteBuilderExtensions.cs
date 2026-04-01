using Granit.ReferenceData.Domain;
using Granit.ReferenceData.Endpoints.Endpoints;
using Granit.ReferenceData.Endpoints.Options;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

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
    /// For example, <c>MapGranitReferenceData&lt;Country&gt;()</c> creates routes under
    /// <c>/reference-data/country</c>.
    /// </para>
    /// <para>Call from your application:</para>
    /// <code>
    /// app.MapGranitReferenceData&lt;Country&gt;();
    /// app.MapGranitReferenceData&lt;Currency&gt;(opts => opts.TagName = "Currencies");
    /// </code>
    /// </remarks>
    public static RouteGroupBuilder MapGranitReferenceData<TEntity>(
        this IEndpointRouteBuilder endpoints,
        Action<ReferenceDataEndpointsOptions>? configure = null)
        where TEntity : ReferenceDataEntity, new()
    {
        ReferenceDataEndpointsOptions options = new();
        configure?.Invoke(options);

        string entitySegment = ToKebabCase(typeof(TEntity).Name);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup($"{options.RoutePrefix}/{entitySegment}")
            .WithTags(options.TagName);

        group.MapReadEndpoints<TEntity>();
        group.MapAdminEndpoints<TEntity>();

        return group;
    }

    /// <summary>
    /// Maps reference data endpoints for a single dynamically registered type by name.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="typeName">
    /// The logical type name (e.g., <c>"Countries"</c>) as declared in
    /// <c>AddReferenceData&lt;TDbContext&gt;()</c>.
    /// </param>
    /// <param name="configure">Optional delegate to customize <see cref="ReferenceDataEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapGranitReferenceData(
        this IEndpointRouteBuilder endpoints,
        string typeName,
        Action<ReferenceDataEndpointsOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);

        ReferenceDataEndpointsOptions options = new();
        configure?.Invoke(options);

        string entitySegment = ToKebabCase(typeName);

        RouteGroupBuilder group = endpoints
            .MapGranitGroup($"{options.RoutePrefix}/{entitySegment}")
            .WithTags(options.TagName);

        group.MapDynamicReadEndpoints(typeName);
        group.MapDynamicAdminEndpoints(typeName);

        return group;
    }

    /// <summary>
    /// Maps reference data endpoints for all dynamically registered types in the
    /// <see cref="ReferenceDataRegistry"/>.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize options for all types.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapGranitAllReferenceData(
        this IEndpointRouteBuilder endpoints,
        Action<ReferenceDataEndpointsOptions>? configure = null)
    {
        ReferenceDataRegistry registry = endpoints.ServiceProvider.GetRequiredService<ReferenceDataRegistry>();

        foreach (ReferenceDataTypeRegistration registration in registry.Types)
        {
            endpoints.MapGranitReferenceData(registration.TypeName, configure);
        }

        return endpoints;
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
