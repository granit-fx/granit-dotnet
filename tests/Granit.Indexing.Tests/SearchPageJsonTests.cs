using System.Text.Json;
using Shouldly;
using Xunit;

namespace Granit.Indexing.Tests;

public sealed class SearchPageJsonTests
{
    [Fact]
    public void BackendHitCount_is_never_serialized()
    {
        SearchPage<string> page = new()
        {
            Items = ["a", "b"],
            Page = 1,
            PageSize = 20,
            TotalAuthorized = 2,
            HitAuthorizationLimit = true,
            BackendHitCount = 1337,
        };

        string json = JsonSerializer.Serialize(page);

        json.ShouldNotContain("backendHitCount", Case.Insensitive);
        json.ShouldNotContain("1337");
    }

    [Fact]
    public void Round_trip_preserves_public_state_but_drops_backend_hit_count()
    {
        SearchPage<string> original = new()
        {
            Items = ["a"],
            Page = 2,
            PageSize = 50,
            TotalAuthorized = 1,
            HitAuthorizationLimit = false,
            BackendHitCount = 99,
        };

        string json = JsonSerializer.Serialize(original);
        SearchPage<string>? deserialized = JsonSerializer.Deserialize<SearchPage<string>>(json);

        deserialized.ShouldNotBeNull();
        deserialized.Items.ShouldBe(["a"]);
        deserialized.Page.ShouldBe(2);
        deserialized.PageSize.ShouldBe(50);
        deserialized.TotalAuthorized.ShouldBe(1);
        deserialized.HitAuthorizationLimit.ShouldBeFalse();
        deserialized.BackendHitCount.ShouldBe(0); // dropped on the wire
    }
}
