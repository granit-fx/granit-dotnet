using Granit.Authorization;
using Granit.Workflow.Notifications.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workflow.Notifications.Extensions;

/// <summary>
/// Extension methods for registering Granit.Workflow.Notifications services.
/// </summary>
public static class WorkflowNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the workflow approval notification bridge services.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers a <see cref="NullApproverResolver"/> as the default
    /// <see cref="IApproverResolver"/>. The host application should replace this
    /// with a real implementation via <see cref="AddIdentityApproverResolver"/> or
    /// <see cref="AddWorkflowApproverResolver{TResolver}"/>.
    /// </para>
    /// <para>
    /// The <see cref="Handlers.WorkflowApprovalRequestedHandler"/> is discovered
    /// automatically by Wolverine handler scanning.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitWorkflowNotifications(
        this IServiceCollection services)
    {
        services.TryAddScoped<IApproverResolver, NullApproverResolver>();
        return services;
    }

    /// <summary>
    /// Registers a custom <see cref="IApproverResolver"/> implementation for
    /// resolving users authorized to approve workflow transitions.
    /// </summary>
    /// <typeparam name="TResolver">The approver resolver implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWorkflowApproverResolver<TResolver>(
        this IServiceCollection services)
        where TResolver : class, IApproverResolver
    {
        services.Replace(ServiceDescriptor.Scoped<IApproverResolver, TResolver>());
        return services;
    }

    /// <summary>
    /// Registers the built-in identity-based approver resolver.
    /// Resolves approvers by: permission → roles (via <c>IPermissionManagerReader</c>) →
    /// role members (via <see cref="Granit.Identity.IIdentityProvider"/>).
    /// </summary>
    /// <remarks>
    /// Requires both <c>Granit.Authorization</c> (for <c>IPermissionManagerReader</c>) and an
    /// <see cref="Granit.Identity.IIdentityProvider"/> implementation (e.g.
    /// <c>Granit.Identity.Federated.Keycloak</c>) to be registered.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdentityApproverResolver(
        this IServiceCollection services)
    {
        services.AddWorkflowApproverResolver<IdentityApproverResolver>();
        return services;
    }
}
