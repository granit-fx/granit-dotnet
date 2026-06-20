using Granit.Mentions.Exceptions;
using Granit.Mentions.Internal;
using Shouldly;

namespace Granit.Mentions.Tests;

public sealed class MentionRegistryTests
{
    private sealed class StubResolver(string type) : IMentionResolver
    {
        public string Type => type;

        public ValueTask<IReadOnlyList<MentionSuggestion>> SearchAsync(string query, int limit, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IReadOnlyList<MentionSuggestion>>([]);

        public ValueTask<MentionTarget?> ResolveAsync(string id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<MentionTarget?>(null);
    }

    [Fact]
    public void TryGet_resolves_by_type_case_insensitively()
    {
        var registry = new MentionRegistry([new StubResolver("invoice")]);

        registry.TryGet("INVOICE", out IMentionResolver? resolver).ShouldBeTrue();
        resolver!.Type.ShouldBe("invoice");
    }

    [Fact]
    public void TryGet_returns_false_for_unregistered_type()
    {
        var registry = new MentionRegistry([new StubResolver("invoice")]);

        registry.TryGet("ledger", out IMentionResolver? resolver).ShouldBeFalse();
        resolver.ShouldBeNull();
    }

    [Fact]
    public void Duplicate_type_throws()
    {
        Should.Throw<DuplicateMentionResolverException>(
            () => new MentionRegistry([new StubResolver("invoice"), new StubResolver("invoice")]));
    }

    [Fact]
    public void Blank_type_throws()
    {
        Should.Throw<InvalidMentionTypeException>(() => new MentionRegistry([new StubResolver("  ")]));
    }
}
