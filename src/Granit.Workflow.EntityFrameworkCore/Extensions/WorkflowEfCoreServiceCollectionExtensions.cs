using Granit.QueryEngine;
using Granit.Workflow.Domain;
using Granit.Workflow.EntityFrameworkCore.Interceptors;
using Granit.Workflow.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workflow.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering Granit.Workflow EF Core services.
/// </summary>
public static class WorkflowEfCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="WorkflowTransitionInterceptor"/> as a scoped service
    /// for automatic ISO 27001 audit trail creation on workflow state transitions.
    /// Also registers <see cref="IWorkflowHistoryQuery"/> and
    /// <see cref="IWorkflowTransitionRecorder"/> backed by <typeparamref name="TDbContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host application's DbContext must implement <see cref="IWorkflowDbContext"/>
    /// and call <c>modelBuilder.ConfigureWorkflowModule()</c> in <c>OnModelCreating</c>.
    /// </para>
    /// <para>
    /// The interceptor must be ordered after <c>AuditedEntityInterceptor</c> and before
    /// <c>SoftDeleteInterceptor</c> in the interceptor chain.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDbContext">
    /// The host application's DbContext implementing <see cref="IWorkflowDbContext"/>.
    /// </typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitWorkflowEntityFrameworkCore<TDbContext>(
        this IServiceCollection services)
        where TDbContext : DbContext, IWorkflowDbContext
    {
        services.TryAddScoped<WorkflowTransitionInterceptor>();
        services.TryAddScoped<IWorkflowHistoryQuery, DefaultWorkflowHistoryQuery<TDbContext>>();
        services.TryAddScoped<IWorkflowTransitionRecorder, EfWorkflowTransitionRecorder<TDbContext>>();

        // Backs MapGranitQuery<WorkflowTransitionRecord> + the analytics runner over
        // WorkflowTransitionRecordQuery, projecting from the host's IWorkflowDbContext.
        services.TryAddScoped<IQueryableSource<WorkflowTransitionRecord>,
            EfWorkflowTransitionRecordQueryableSource<TDbContext>>();
        return services;
    }

    /// <summary>
    /// Registers the <see cref="WorkflowTransitionInterceptor"/> without the query/recorder services.
    /// Use <see cref="AddGranitWorkflowEntityFrameworkCore{TDbContext}"/> instead when possible.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitWorkflowEntityFrameworkCore(
        this IServiceCollection services)
    {
        services.TryAddScoped<WorkflowTransitionInterceptor>();
        return services;
    }
}
