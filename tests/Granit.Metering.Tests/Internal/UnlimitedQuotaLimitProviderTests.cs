using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Internal;
using Shouldly;
using Xunit;

namespace Granit.Metering.Tests.Internal;

public sealed class UnlimitedQuotaLimitProviderTests
{
    private readonly UnlimitedQuotaLimitProvider _sut = new();

    // ======== Always returns null ========

    [Fact]
    public async Task GetLimitAsync_ShouldReturnNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();
        var meterId = MeterDefinitionId.Create(Guid.NewGuid());

        decimal? result = await _sut.GetLimitAsync(tenantId, meterId, ct);

        result.ShouldBeNull();
    }

    // ======== Consistent across tenants ========

    [Fact]
    public async Task GetLimitAsync_DifferentTenants_ShouldAlwaysReturnNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var meterId = MeterDefinitionId.Create(Guid.NewGuid());

        decimal? result1 = await _sut.GetLimitAsync(Guid.NewGuid(), meterId, ct);
        decimal? result2 = await _sut.GetLimitAsync(Guid.NewGuid(), meterId, ct);

        result1.ShouldBeNull();
        result2.ShouldBeNull();
    }

    // ======== Consistent across meters ========

    [Fact]
    public async Task GetLimitAsync_DifferentMeters_ShouldAlwaysReturnNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        var tenantId = Guid.NewGuid();

        decimal? result1 = await _sut.GetLimitAsync(tenantId, MeterDefinitionId.Create(Guid.NewGuid()), ct);
        decimal? result2 = await _sut.GetLimitAsync(tenantId, MeterDefinitionId.Create(Guid.NewGuid()), ct);

        result1.ShouldBeNull();
        result2.ShouldBeNull();
    }
}
