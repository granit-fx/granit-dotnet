using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class TenantEnumeratorDataSeedTenantProviderTests
{
    [Fact]
    public async Task GetTenantIdsAsync_DelegatesToTenantEnumerator()
    {
        // Arrange
        var tenant1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var tenant2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(tenant1, tenant2));

        TenantEnumeratorDataSeedTenantProvider sut = new(enumerator);

        // Act
        List<Guid> result = [];
        await foreach (Guid id in sut.GetTenantIdsAsync(TestContext.Current.CancellationToken))
        {
            result.Add(id);
        }

        // Assert
        result.ShouldBe([tenant1, tenant2]);
    }

    [Fact]
    public async Task GetTenantIdsAsync_EmptyEnumerator_ReturnsEmpty()
    {
        // Arrange
        ITenantEnumerator enumerator = Substitute.For<ITenantEnumerator>();
        enumerator.GetActiveTenantIdsAsync(Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable());

        TenantEnumeratorDataSeedTenantProvider sut = new(enumerator);

        // Act
        List<Guid> result = [];
        await foreach (Guid id in sut.GetTenantIdsAsync(TestContext.Current.CancellationToken))
        {
            result.Add(id);
        }

        // Assert
        result.ShouldBeEmpty();
    }

    private static async IAsyncEnumerable<Guid> ToAsyncEnumerable(params Guid[] ids)
    {
        foreach (Guid id in ids)
        {
            yield return id;
        }

        await Task.CompletedTask;
    }
}
