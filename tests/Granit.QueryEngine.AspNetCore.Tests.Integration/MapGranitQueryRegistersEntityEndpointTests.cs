using Granit.Entities;
using Granit.MultiTenancy;
using Granit.QueryEngine.AspNetCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.AspNetCore.Tests.Integration;

/// <summary>
/// Verifies that <c>MapGranitQuery&lt;TEntity&gt;</c> tags its list endpoint with
/// <see cref="EntityEndpointMetadata"/> so the entity-discovery surface
/// (<c>Granit.Entities.Endpoints</c>) can walk <see cref="EndpointDataSource"/>
/// and expose the real URL on <c>EntityDiscoveryLinks.List</c> — same mechanism
/// ASP.NET's own OpenAPI generator uses for endpoint metadata lookups.
/// </summary>
public sealed class MapGranitQueryRegistersEntityEndpointTests
{
    [Fact]
    public async Task Tags_list_endpoint_with_metadata_carrying_resolved_path()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(Substitute.For<IQueryEngine<TestProduct>>());
        builder.Services.AddSingleton<QueryDefinition<TestProduct>, TestProductQueryDefinition>();
        builder.Services.AddSingleton<ICurrentTenant>(Substitute.For<ICurrentTenant>());

        await using WebApplication app = builder.Build();

        app.MapGranitQuery<TestProduct>(
            _ => Array.Empty<TestProduct>().AsQueryable(),
            "/api/v1/products",
            opts => opts.AllowAnonymous = true);

        await app.StartAsync(TestContext.Current.CancellationToken);

        EndpointDataSource source = app.Services.GetRequiredService<EndpointDataSource>();

        (RouteEndpoint endpoint, EntityEndpointMetadata meta) =
            source.Endpoints
                .OfType<RouteEndpoint>()
                .Select(e => (Endpoint: e, Meta: e.Metadata.GetMetadata<EntityEndpointMetadata>()))
                .Where(x => x.Meta is not null
                    && x.Meta.EntityType == typeof(TestProduct)
                    && x.Meta.Kind == EntityEndpointKind.List)
                .Select(x => (x.Endpoint, Meta: x.Meta!))
                .ShouldHaveSingleItem();

        endpoint.RoutePattern.RawText.ShouldBe("/api/v1/products/");
        meta.EntityType.ShouldBe(typeof(TestProduct));
        meta.Kind.ShouldBe(EntityEndpointKind.List);
    }
}
