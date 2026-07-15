using Granit.Identity.Local.Domain;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.OpenIddict.Models;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

/// <summary>
/// Regression: the identity entities owned by the consolidated <c>OpenIddictDbContext</c>
/// (<see cref="GranitRole"/>, <see cref="GranitUserGroup"/>, <see cref="OpenIddictApplicationModel"/>,
/// <see cref="OpenIddictScopeModel"/>) must each have a resolvable
/// <see cref="IQueryableSource{T}"/>, otherwise <c>GranitRoleQuery</c> / <c>GranitUserGroupQuery</c>
/// / <c>ApplicationQuery</c> / <c>ScopeQuery</c> grids and analytics runners throw
/// "No service for type IQueryableSource&lt;T&gt;" at first request.
/// </summary>
public sealed class OpenIddictEfCoreQueryableSourceRegistrationTests
{
    private static IServiceCollection Register()
    {
        // Development environment so AddGranitOpenIddictServer accepts ephemeral signing keys
        // (forbidden elsewhere) — registration would otherwise throw before reaching the
        // IQueryableSource registrations.
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        // The provider callback is only invoked at context creation, not registration — an
        // empty configure is enough to assert the DI descriptors without an EF provider package.
        builder.AddGranitOpenIddict(_ => { });
        return builder.Services;
    }

    [Theory]
    [InlineData(typeof(GranitRole))]
    [InlineData(typeof(GranitUserGroup))]
    [InlineData(typeof(OpenIddictApplicationModel))]
    [InlineData(typeof(OpenIddictScopeModel))]
    public void AddGranitOpenIddict_RegistersQueryableSource_AsScoped(Type entityType)
    {
        IServiceCollection services = Register();

        Type sourceType = typeof(IQueryableSource<>).MakeGenericType(entityType);

        services.ShouldContain(d =>
            d.ServiceType == sourceType &&
            d.Lifetime == ServiceLifetime.Scoped);
    }
}
