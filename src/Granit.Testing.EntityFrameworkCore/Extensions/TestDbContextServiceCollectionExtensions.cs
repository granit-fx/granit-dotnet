using Granit.Core.MultiTenancy;
using Granit.Guids;
using Granit.Persistence.Interceptors;
using Granit.Security;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Testing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods on <see cref="IServiceCollection"/> for registering
/// test DbContext instances in a <see cref="GranitTestFixture{TModule}"/>.
/// </summary>
public static class TestDbContextServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> as a scoped service backed by
    /// the EF Core In-Memory provider with Granit interceptors.
    /// </summary>
    /// <remarks>
    /// Resolves <see cref="FakeCurrentTenant"/>, <see cref="FakeCurrentUser"/>,
    /// <see cref="FakeClock"/>, and <see cref="FakeGuidGenerator"/> from the
    /// service provider to wire the interceptors. These are typically registered
    /// by <see cref="GranitTestFixture{TModule}"/>.
    /// </remarks>
    /// <typeparam name="TContext">The <see cref="DbContext"/> type to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitTestDbContext<TContext>(
        this IServiceCollection services)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDbContext<TContext>((sp, options) =>
        {
            ICurrentTenant tenant = sp.GetRequiredService<ICurrentTenant>();
            ICurrentUserService user = sp.GetRequiredService<ICurrentUserService>();
            IClock clock = sp.GetRequiredService<IClock>();
            IGuidGenerator guidGenerator = sp.GetRequiredService<IGuidGenerator>();

            AuditedEntityInterceptor auditInterceptor = new(user, clock, guidGenerator, tenant);
            VersioningInterceptor versioningInterceptor = new(guidGenerator);
            ConcurrencyStampInterceptor concurrencyStampInterceptor = new();
            SoftDeleteInterceptor softDeleteInterceptor = new(user, clock);

            options.UseInMemoryDatabase(guidGenerator.Create().ToString())
                .AddInterceptors(auditInterceptor, versioningInterceptor, concurrencyStampInterceptor, softDeleteInterceptor);
        });

        return services;
    }
}
