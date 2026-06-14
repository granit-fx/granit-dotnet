using Granit.AI.Chat.Exceptions;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Mentions;
using Shouldly;

namespace Granit.AI.Chat.Tests;

public sealed class AIMentionRegistryTests
{
    private sealed class StubResolver(string type) : IAIMentionResolver
    {
        public string Type => type;

        public ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIMentionContext?>(null);
    }

    [Fact]
    public void TryGet_resolves_by_type_case_insensitively()
    {
        var registry = new AIMentionRegistry([new StubResolver("invoice")]);

        registry.TryGet("INVOICE", out IAIMentionResolver? resolver).ShouldBeTrue();
        resolver!.Type.ShouldBe("invoice");
    }

    [Fact]
    public void TryGet_returns_false_for_unregistered_type()
    {
        var registry = new AIMentionRegistry([new StubResolver("invoice")]);

        registry.TryGet("ledger", out IAIMentionResolver? resolver).ShouldBeFalse();
        resolver.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_type_throws()
    {
        Should.Throw<DuplicateMentionResolverException>(
            () => new AIMentionRegistry([new StubResolver("invoice"), new StubResolver("invoice")]));
    }

    [Fact]
    public void Blank_type_throws()
    {
        Should.Throw<InvalidMentionTypeException>(
            () => new AIMentionRegistry([new StubResolver("  ")]));
    }
}
