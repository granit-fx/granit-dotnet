using Granit.AI.Endpoints.Endpoints;
using Granit.AI.Endpoints.Internal;
using Granit.AI.Endpoints.Options;
using Granit.QueryEngine.Endpoints.Extensions;
using Granit.Validation.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Granit.AI.Endpoints.Extensions;

/// <summary>
/// Extension methods for registering AI endpoints.
/// </summary>
public static class AIEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps AI administration and inference endpoints onto the given route builder.
    /// </summary>
    /// <remarks>
    /// <para>Call this from your application route registration:</para>
    /// <code>
    /// app.MapAIEndpoints();
    ///
    /// // With custom options:
    /// app.MapAIEndpoints(opts =>
    /// {
    ///     opts.RoutePrefix = "api/ai";
    ///     opts.AdminRole = "ai-admin";
    /// });
    /// </code>
    /// </remarks>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <param name="configure">Optional delegate to customize <see cref="AIEndpointsOptions"/>.</param>
    /// <returns>The <see cref="RouteGroupBuilder"/> for further chaining.</returns>
    public static RouteGroupBuilder MapAIEndpoints(
        this IEndpointRouteBuilder endpoints,
        Action<AIEndpointsOptions>? configure = null)
    {
        AIEndpointsOptions options = new();
        configure?.Invoke(options);

        IOptions<AuthorizationOptions> authOptions =
            endpoints.ServiceProvider.GetRequiredService<IOptions<AuthorizationOptions>>();
        authOptions.Value.AddPolicy(
            AIAuthorizationPolicy.AdminPolicyName,
            policy => policy.RequireRole(options.AdminRole));
        authOptions.Value.AddPolicy(
            AIAuthorizationPolicy.UserPolicyName,
            policy => policy.RequireRole(options.UserRole));

        RouteGroupBuilder group = endpoints.MapGranitGroup(options.RoutePrefix);

        // Admin endpoints — workspace CRUD
        RouteGroupBuilder adminGroup = group.MapGroup("")
            .WithTags(options.WorkspacesTagName)
            .RequireAuthorization(AIAuthorizationPolicy.AdminPolicyName);
        adminGroup.MapWorkspaceEndpoints();

        // Admin endpoints — usage tracking via Granit.QueryEngine.
        // Use a temporary scope because IAIUsageQueryableProvider is Scoped
        // when EF Core persistence is registered and cannot be resolved from the root provider.
        bool hasQueryableProvider;
        using (IServiceScope scope = endpoints.ServiceProvider.CreateScope())
        {
            hasQueryableProvider = scope.ServiceProvider.GetService<IAIUsageQueryableProvider>() is not null;
        }

        if (hasQueryableProvider)
        {
            RouteGroupBuilder usageGroup = group.MapGroup("")
                .WithTags(options.UsageTagName)
                .RequireAuthorization(AIAuthorizationPolicy.AdminPolicyName);
            usageGroup.MapQueryEndpoints<AIUsageRecord>(
                "usage/query",
                sp => sp.GetRequiredService<IAIUsageQueryableProvider>().GetUsageRecords());
        }

        // User endpoints — chat completion proxy
        RouteGroupBuilder chatGroup = group.MapGroup("")
            .WithTags(options.InferenceTagName)
            .RequireAuthorization(AIAuthorizationPolicy.UserPolicyName);
        chatGroup.MapChatEndpoints();

        // User endpoints — embedding generation proxy
        RouteGroupBuilder embeddingGroup = group.MapGroup("")
            .WithTags(options.InferenceTagName)
            .RequireAuthorization(AIAuthorizationPolicy.UserPolicyName);
        embeddingGroup.MapEmbeddingEndpoints();

        return group;
    }
}
