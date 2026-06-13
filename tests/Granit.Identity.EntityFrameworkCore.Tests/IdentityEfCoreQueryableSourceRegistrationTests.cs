using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: <c>UserQuery</c> grids and the analytics runner resolve the open generic
/// <see cref="IQueryableSource{User}"/>, distinct from the directory
/// <c>IUserDirectoryQueryableSource</c> contract. Without the explicit registration the
/// query engine throws "No service for type IQueryableSource&lt;User&gt;" at first request.
/// </summary>
public sealed class IdentityEfCoreQueryableSourceRegistrationTests
{
    [Fact]
    public void AddGranitIdentityEntityFrameworkCore_RegistersQueryableSource_ForUser_AsScoped()
    {
        var services = new ServiceCollection();

        // The provider callback is only invoked at context creation, not registration — an
        // empty configure is enough to assert the DI descriptors without an EF provider package.
        services.AddGranitIdentityEntityFrameworkCore(_ => { });

        services.ShouldContain(d =>
            d.ServiceType == typeof(IQueryableSource<User>) &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
