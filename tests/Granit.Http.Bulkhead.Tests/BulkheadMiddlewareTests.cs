using System.Diagnostics.Metrics;
using Granit.Core.MultiTenancy;
using Granit.Http.Bulkhead.Abstractions;
using Granit.Http.Bulkhead.Attributes;
using Granit.Http.Bulkhead.Diagnostics;
using Granit.Http.Bulkhead.Internal;
using Granit.Http.Bulkhead.Options;
using Granit.Http.Bulkhead.Wolverine;
using Granit.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Http.Bulkhead.Tests;

public sealed class BulkheadMiddlewareTests : IDisposable
{
    private readonly IBulkheadQuotaProvider _quotaProvider = Substitute.For<IBulkheadQuotaProvider>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ConcurrencyLimiterRegistry _registry;
    private readonly IMeterFactory _meterFactory;

    public BulkheadMiddlewareTests()
    {
        _registry = new ConcurrencyLimiterRegistry(TimeProvider.System);
        _currentTenant.IsAvailable.Returns(false);
        _quotaProvider.GetPermitLimitAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((int?)null);
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

    private TenantPartitionedBulkhead CreateBulkhead(GranitBulkheadOptions? options = null)
    {
        options ??= new GranitBulkheadOptions
        {
            Enabled = true,
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["import"] = new() { PermitLimit = 5, QueueLimit = 0 },
            },
        };

        return new TenantPartitionedBulkhead(
            _registry,
            _quotaProvider,
            MsOptions.Create(options),
            _currentTenant,
            _currentUser,
            new BulkheadMetrics(_meterFactory),
            NullLogger<TenantPartitionedBulkhead>.Instance);
    }

    // =========================================================================
    // No attribute
    // =========================================================================

    [Fact]
    public async Task BeforeAsync_NoAttribute_ReturnsNoOp()
    {
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();
        var message = new MessageWithoutAttribute();

        BulkheadLease lease = await BulkheadMiddleware.BeforeAsync(message, bulkhead, TestContext.Current.CancellationToken);

        lease.ShouldBeSameAs(BulkheadLease.NoOp);
    }

    // =========================================================================
    // With attribute — acquire + dispose
    // =========================================================================

    [Fact]
    public async Task BeforeAsync_WithAttribute_AcquiresLease()
    {
        TenantPartitionedBulkhead bulkhead = CreateBulkhead();
        var message = new ImportCommand();

        BulkheadLease lease = await BulkheadMiddleware.BeforeAsync(message, bulkhead, TestContext.Current.CancellationToken);

        lease.IsAcquired.ShouldBeTrue();
        lease.ShouldNotBeSameAs(BulkheadLease.NoOp);

        BulkheadMiddleware.After(lease);
    }

    [Fact]
    public async Task After_DisposesLease()
    {
        TenantPartitionedBulkhead bulkhead = CreateBulkhead(new GranitBulkheadOptions
        {
            Enabled = true,
            Policies = new Dictionary<string, BulkheadPolicyOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["import"] = new() { PermitLimit = 1, QueueLimit = 0 },
            },
        });

        var message = new ImportCommand();

        BulkheadLease lease = await BulkheadMiddleware.BeforeAsync(message, bulkhead, TestContext.Current.CancellationToken);
        BulkheadMiddleware.After(lease);

        // After disposing, we should be able to acquire again
        BulkheadLease lease2 = await BulkheadMiddleware.BeforeAsync(message, bulkhead, TestContext.Current.CancellationToken);
        lease2.IsAcquired.ShouldBeTrue();
        BulkheadMiddleware.After(lease2);
    }

    // =========================================================================
    // Test messages
    // =========================================================================

    private sealed class MessageWithoutAttribute;

    [Bulkhead("import")]
    private sealed class ImportCommand;
}
