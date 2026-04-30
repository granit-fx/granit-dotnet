using System.Security.Claims;
using Granit.Entities.Relations;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests;

public sealed class NullRelationAggregateServiceTests
{
    [Fact]
    public async Task ComputeAsync_returns_empty_dictionary_for_any_input()
    {
        NullRelationAggregateService service = new();
        ClaimsPrincipal user = new(new ClaimsIdentity());

        IReadOnlyDictionary<string, RelationAggregateValue> result = await service.ComputeAsync(
            "Granit.Parties.Party", "any-id", ["invoices", "addresses"],
            user, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }
}
