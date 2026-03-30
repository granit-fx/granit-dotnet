using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class NullTenantEnumeratorTests
{
    [Fact]
    public async Task GetActiveTenantIdsAsync_ReturnsEmptyStream()
    {
        NullTenantEnumerator sut = new();
        List<Guid> result = [];

        await foreach (Guid id in sut.GetActiveTenantIdsAsync(TestContext.Current.CancellationToken))
        {
            result.Add(id);
        }

        result.ShouldBeEmpty();
    }
}
