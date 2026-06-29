using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// <see cref="IModelCacheKeyFactory"/> that folds the registered <see cref="IGranitModelExtension"/> set into
/// the model cache key. EF Core caches a built model per context type; without this, two contexts of the same
/// type configured with <i>different</i> extension sets (a multi-provider host, or independent tests in one
/// process) would share whichever model was built first. Including the extension signature keeps each
/// distinct configuration on its own cached model. Equivalent to the default key when no extensions exist.
/// </summary>
public sealed class GranitModelCacheKeyFactory : IModelCacheKeyFactory
{
    /// <inheritdoc />
    public object Create(DbContext context, bool designTime)
    {
        ArgumentNullException.ThrowIfNull(context);

        IServiceProvider? applicationServices = context.GetService<IDbContextOptions>()
            .FindExtension<CoreOptionsExtension>()?.ApplicationServiceProvider;

        string extensionSignature = applicationServices is null
            ? string.Empty
            : string.Join(
                ',',
                applicationServices.GetServices<IGranitModelExtension>()
                    .Select(static extension => extension.GetType().FullName)
                    .OrderBy(static name => name, StringComparer.Ordinal));

        return (context.GetType(), designTime, extensionSignature);
    }
}
