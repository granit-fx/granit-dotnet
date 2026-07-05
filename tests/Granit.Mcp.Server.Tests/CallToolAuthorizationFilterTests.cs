using Granit.Authorization;
using Granit.Authorization.Attributes;
using Granit.Mcp.Server.Internal;
using Granit.Mcp.Server.Permissions;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using NSubstitute;
using Shouldly;

namespace Granit.Mcp.Server.Tests;

/// <summary>
/// Proves the deny-by-default behaviour of <see cref="CallToolAuthorizationFilter"/>:
/// an un-annotated tool is rejected unless the principal holds
/// <see cref="McpPermissions.Tools.Execute"/>; an explicitly authorized tool passes
/// through (the SDK filter enforces it); a tenant-scoped tool is rejected without a tenant.
/// </summary>
public sealed class CallToolAuthorizationFilterTests
{
    private const string PlainToolName = nameof(PlainTool);
    private const string AuthorizedToolName = nameof(AuthorizedTool);
    private const string TenantScopedToolName = nameof(TenantScopedTool);

    [Fact]
    public async Task EvaluateAsync_UnresolvedTool_IsDenied()
    {
        await using ServiceProvider sp = BuildServices();

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, "ghost-tool", matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task EvaluateAsync_UnannotatedTool_WithoutExecutePermission_IsDenied()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(McpPermissions.Tools.Execute, Arg.Any<CancellationToken>())
            .Returns(false);

        await using ServiceProvider sp = BuildServices(permissionChecker: checker);

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, PlainToolName, matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task EvaluateAsync_UnannotatedTool_WithNoPermissionChecker_IsDenied()
    {
        await using ServiceProvider sp = BuildServices(permissionChecker: null);

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, PlainToolName, matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task EvaluateAsync_UnannotatedTool_WithExecutePermission_IsAllowed()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(McpPermissions.Tools.Execute, Arg.Any<CancellationToken>())
            .Returns(true);

        await using ServiceProvider sp = BuildServices(permissionChecker: checker);

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, PlainToolName, matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_ExplicitlyAuthorizedTool_IsAllowed_WithoutExecutePermission()
    {
        // No IPermissionChecker registered: the tool must pass through purely because it
        // carries explicit authorization metadata (enforced downstream by the SDK filter).
        await using ServiceProvider sp = BuildServices(permissionChecker: null);

        object[] metadata = [new PermissionAttribute("Some.Resource.Action")];

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, AuthorizedToolName, metadata, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_TenantScopedTool_WithoutTenant_IsDenied()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(McpPermissions.Tools.Execute, Arg.Any<CancellationToken>())
            .Returns(true);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);

        await using ServiceProvider sp = BuildServices(permissionChecker: checker, currentTenant: tenant);

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, TenantScopedToolName, matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsError.ShouldBe(true);
    }

    [Fact]
    public async Task EvaluateAsync_TenantScopedTool_WithTenant_AndExecutePermission_IsAllowed()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(McpPermissions.Tools.Execute, Arg.Any<CancellationToken>())
            .Returns(true);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);

        await using ServiceProvider sp = BuildServices(permissionChecker: checker, currentTenant: tenant);

        CallToolResult? result = await CallToolAuthorizationFilter.EvaluateAsync(
            sp, TenantScopedToolName, matchedPrimitiveMetadata: null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    private static ServiceProvider BuildServices(
        IPermissionChecker? permissionChecker = null,
        ICurrentTenant? currentTenant = null)
    {
        McpToolTypeRegistry registry = new();
        registry.Register(PlainToolName, typeof(PlainTool));
        registry.Register(AuthorizedToolName, typeof(AuthorizedTool));
        registry.Register(TenantScopedToolName, typeof(TenantScopedTool));

        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(registry);

        if (permissionChecker is not null)
        {
            services.AddSingleton(permissionChecker);
        }

        if (currentTenant is not null)
        {
            services.AddSingleton(currentTenant);
        }

        return services.BuildServiceProvider();
    }

    private sealed class PlainTool;

    private sealed class AuthorizedTool;

    [McpTenantScope(RequireTenant = true)]
    private sealed class TenantScopedTool;
}
