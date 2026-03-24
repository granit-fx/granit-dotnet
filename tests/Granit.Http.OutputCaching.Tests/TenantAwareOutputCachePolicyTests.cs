using Granit.Http.OutputCaching.Policies;
using Granit.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Http.OutputCaching.Tests;

public sealed class TenantAwareOutputCachePolicyTests
{
    private readonly TenantAwareOutputCachePolicy _sut = new();

    [Fact]
    public async Task CacheRequestAsync_WhenTenantAvailable_AddsVaryByTenantId()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);

        OutputCacheContext context = CreateContext(tenant);

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.CacheVaryByRules.VaryByValues
            .ShouldContainKey(TenantAwareOutputCachePolicy.TenantVaryByKey);
        context.CacheVaryByRules.VaryByValues[TenantAwareOutputCachePolicy.TenantVaryByKey]
            .ShouldBe(tenantId.ToString());
    }

    [Fact]
    public async Task CacheRequestAsync_WhenTenantAvailable_AddsTenantTag()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(tenantId);

        OutputCacheContext context = CreateContext(tenant);

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.Tags.ShouldContain($"{TenantAwareOutputCachePolicy.TenantTagPrefix}{tenantId}");
    }

    [Fact]
    public async Task CacheRequestAsync_WhenNoTenantService_DoesNotAddVaryBy()
    {
        // Arrange — no ICurrentTenant registered
        OutputCacheContext context = CreateContext(tenant: null);

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.CacheVaryByRules.VaryByValues
            .ShouldNotContainKey(TenantAwareOutputCachePolicy.TenantVaryByKey);
        context.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task CacheRequestAsync_WhenTenantNotAvailable_DoesNotAddVaryBy()
    {
        // Arrange
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        tenant.Id.Returns((Guid?)null);

        OutputCacheContext context = CreateContext(tenant);

        // Act
        await _sut.CacheRequestAsync(context, CancellationToken.None);

        // Assert
        context.CacheVaryByRules.VaryByValues
            .ShouldNotContainKey(TenantAwareOutputCachePolicy.TenantVaryByKey);
        context.Tags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ServeFromCacheAsync_IsNoOp()
    {
        OutputCacheContext context = CreateContext(tenant: null);
        bool originalCaching = context.EnableOutputCaching;

        await _sut.ServeFromCacheAsync(context, CancellationToken.None);

        context.EnableOutputCaching.ShouldBe(originalCaching);
    }

    [Fact]
    public async Task ServeResponseAsync_IsNoOp()
    {
        OutputCacheContext context = CreateContext(tenant: null);
        bool originalCaching = context.EnableOutputCaching;

        await _sut.ServeResponseAsync(context, CancellationToken.None);

        context.EnableOutputCaching.ShouldBe(originalCaching);
    }

    private static OutputCacheContext CreateContext(ICurrentTenant? tenant)
    {
        ServiceCollection services = [];
        if (tenant is not null)
        {
            services.AddSingleton(tenant);
        }

        DefaultHttpContext httpContext = new()
        {
            RequestServices = services.BuildServiceProvider(),
        };

        return new OutputCacheContext { HttpContext = httpContext };
    }
}
