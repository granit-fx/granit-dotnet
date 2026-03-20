using System.Diagnostics.Metrics;
using Granit.Core.MultiTenancy;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Exceptions;
using Granit.Http.Bulkhead.Internal;
using Granit.Http.Bulkhead.Options;
using Granit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Http.Bulkhead.Tests;

public sealed class TenantPartitionedBulkheadTests : IDisposable
{
    private readonly IBulkheadQuotaProvider _quotaProvider = Substitute.For<IBulkheadQuotaProvider>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ConcurrencyLimiterRegistry _registry;
    private readonly IMeterFactory _meterFactory;

    private GranitBulkheadOptions _options = new()
    {
        Enabled = true,
        Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["api"] = new() { PermitLimit = 10, QueueLimit = 0 },
        },
    };

    public TenantPartitionedBulkheadTests()
    {
        _registry = new ConcurrencyLimiterRegistry(TimeProvider.System);
        ServiceCollection services = new();
        services.AddMetrics();
        ServiceProvider sp = services.BuildServiceProvider();
        _meterFactory = sp.GetRequiredService<IMeterFactory>();
    }

    public void Dispose()
    {
        _registry.Dispose();
        (_meterFactory as IDisposable)?.Dispose();
    }

    private TenantPartitionedBulkhead CreateBulkhead() =>
        new(
            _registry,
            _quotaProvider,
            MsOptions.Create(_options),
            _currentTenant,
            _currentUser,
            new BulkheadMetrics(_meterFactory),
            NullLogger<TenantPartitionedBulkhead>.Instance);

    // =========================================================================
    // Disabled
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_Disabled_ReturnsNoOp()
    {
        _options.Enabled = false;
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.ShouldBeSameAs(BulkheadLease.NoOp);
    }

    // =========================================================================
    // Unknown policy
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_UnknownPolicy_ReturnsNoOp()
    {
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("nonexistent", TestContext.Current.CancellationToken);

        lease.ShouldBeSameAs(BulkheadLease.NoOp);
    }

    // =========================================================================
    // Machine bypass
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_MachineActor_ReturnsNoOp()
    {
        _currentUser.IsMachine.Returns(true);
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.ShouldBeSameAs(BulkheadLease.NoOp);
    }

    // =========================================================================
    // Bypass roles
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_UserHasBypassRole_ReturnsNoOp()
    {
        _options.BypassRoles = ["SystemAdmin"];
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.IsInRole("SystemAdmin").Returns(true);
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.ShouldBeSameAs(BulkheadLease.NoOp);
    }

    [Fact]
    public async Task AcquireAsync_UnauthenticatedUser_DoesNotCheckBypassRoles()
    {
        _options.BypassRoles = ["SystemAdmin"];
        _currentUser.IsAuthenticated.Returns(false);
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns((int?)null);
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.ShouldNotBeSameAs(BulkheadLease.NoOp);
        _currentUser.DidNotReceiveWithAnyArgs().IsInRole(default!);
        lease.Dispose();
    }

    // =========================================================================
    // Tenant key partitioning
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_WithTenant_AcquiresSuccessfully()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns((int?)null);
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.IsAcquired.ShouldBeTrue();
        lease.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_WithoutTenant_AcquiresSuccessfully()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns((int?)null);
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        lease.IsAcquired.ShouldBeTrue();
        lease.Dispose();
    }

    // =========================================================================
    // Quota provider integration
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_QuotaProviderReturnsLimit_UsesIt()
    {
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns(1);

        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        // Acquire 1 permit (limit from quota provider = 1)
        BulkheadLease lease1 = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);
        lease1.IsAcquired.ShouldBeTrue();

        // Second should be rejected (quota limit is 1, not the config's 10)
        Should.Throw<BulkheadRejectedException>(async () =>
            await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken));

        lease1.Dispose();
    }

    // =========================================================================
    // Rejection
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_WhenFull_ThrowsBulkheadRejectedException()
    {
        _options.Policies["api"] = new BulkheadPolicyOptions { PermitLimit = 1, QueueLimit = 0 };
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns((int?)null);

        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);

        BulkheadRejectedException ex = await Should.ThrowAsync<BulkheadRejectedException>(
            () => bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken));

        ex.PolicyName.ShouldBe("api");
        ex.PermitLimit.ShouldBe(1);
        ex.QueueLimit.ShouldBe(0);

        lease.Dispose();
    }

    // =========================================================================
    // Lease disposal triggers metrics callback
    // =========================================================================

    [Fact]
    public async Task AcquireAsync_LeaseDispose_ReleasesPermit()
    {
        _options.Policies["api"] = new BulkheadPolicyOptions { PermitLimit = 1, QueueLimit = 0 };
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync("api", Arg.Any<CancellationToken>()).Returns((int?)null);

        TenantPartitionedBulkhead bulkhead = CreateBulkhead();

        BulkheadLease lease1 = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);
        lease1.Dispose();

        // Should be able to acquire again after disposal
        BulkheadLease lease2 = await bulkhead.AcquireAsync("api", TestContext.Current.CancellationToken);
        lease2.IsAcquired.ShouldBeTrue();
        lease2.Dispose();
    }
}
