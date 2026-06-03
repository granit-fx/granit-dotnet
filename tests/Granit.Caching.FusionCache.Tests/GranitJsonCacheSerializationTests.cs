using System.Text.Json;
using Granit.Caching.Internal;
using Granit.Caching.Options;
using Granit.Domain;
using Granit.Json;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

namespace Granit.Caching.FusionCache.Tests;

/// <summary>
/// Proves the cache L2 serializer is domain-aware once the canonical Granit JSON converters are
/// applied (as <c>AddGranitCaching</c> now does): a DTO carrying a <see cref="SingleValueObject{T}"/>
/// and an enum serializes to the same flat shape used by the HTTP pipeline and round-trips through
/// both the bare SystemTextJson serializer and the encrypting decorator (the production path).
/// </summary>
public sealed class GranitJsonCacheSerializationTests
{
    private enum Kind { Alpha, Beta }

    private sealed class TestUrl : SingleValueObject<string>
    {
        public override required string Value { get; init; }

        public static TestUrl Of(string value) => new() { Value = value };
    }

    private sealed record CachedShape(TestUrl Canonical, Kind Kind, string Name);

    private static JsonSerializerOptions GranitOptions() =>
        new JsonSerializerOptions().AddGranitJsonConverters();

    [Fact]
    public void Value_objects_serialize_flat_and_enums_as_names()
    {
        string json = JsonSerializer.Serialize(
            new CachedShape(TestUrl.Of("https://acme.test/a"), Kind.Beta, "home"),
            GranitOptions());

        json.ShouldContain("\"https://acme.test/a\"");   // flattened SingleValueObject
        json.ShouldNotContain("\"Canonical\":{");         // not wrapped as { "Value": ... }
        json.ShouldContain("\"Beta\"");                   // enum as name, not its ordinal
    }

    [Fact]
    public void A_value_object_bearing_dto_round_trips_through_the_json_serializer()
    {
        var serializer = new FusionCacheSystemTextJsonSerializer(GranitOptions());
        var original = new CachedShape(TestUrl.Of("https://acme.test/a"), Kind.Beta, "home");

        byte[] bytes = serializer.Serialize(original);
        CachedShape? round = serializer.Deserialize<CachedShape>(bytes);

        round.ShouldNotBeNull();
        round.Canonical.Value.ShouldBe("https://acme.test/a");
        round.Kind.ShouldBe(Kind.Beta);
        round.Name.ShouldBe("home");
    }

    [Fact]
    public void A_value_object_bearing_dto_round_trips_through_the_encrypting_serializer()
    {
        var encryptor = new AesCacheValueEncryptor(
            Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions
            {
                Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            }));
        var sut = new EncryptingFusionCacheSerializer(
            new FusionCacheSystemTextJsonSerializer(GranitOptions()),
            encryptor,
            new CachingOptions { EncryptValues = true });

        var original = new CachedShape(TestUrl.Of("https://acme.test/b"), Kind.Alpha, "about");
        byte[] bytes = sut.Serialize(original);
        CachedShape? round = sut.Deserialize<CachedShape>(bytes);

        round.ShouldNotBeNull();
        round.Canonical.Value.ShouldBe("https://acme.test/b");
        round.Kind.ShouldBe(Kind.Alpha);
        round.Name.ShouldBe("about");
    }
}
