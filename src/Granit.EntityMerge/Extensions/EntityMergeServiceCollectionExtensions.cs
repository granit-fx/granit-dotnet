using Granit.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.EntityMerge.Extensions;

/// <summary>
/// Registration helpers for <see cref="IReferenceRewriter{TAggregate}"/> plug-ins. Each
/// module that holds a foreign key to an aggregate calls
/// <see cref="AddReferenceRewriter{TAggregate, TRewriter}"/> in its DI extension to
/// participate in the scatter-gather merge orchestration.
/// </summary>
public static class EntityMergeServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TRewriter"/> as an <see cref="IReferenceRewriter{TAggregate}"/>
    /// participant. Lifetime: <c>Scoped</c> — rewriters use a per-request <c>DbContext</c>.
    /// </summary>
    /// <typeparam name="TAggregate">The aggregate whose references the rewriter handles.</typeparam>
    /// <typeparam name="TRewriter">Concrete rewriter implementation.</typeparam>
    public static IServiceCollection AddReferenceRewriter<TAggregate, TRewriter>(
        this IServiceCollection services)
        where TAggregate : Entity
        where TRewriter : class, IReferenceRewriter<TAggregate>
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<IReferenceRewriter<TAggregate>, TRewriter>();
        return services;
    }
}
