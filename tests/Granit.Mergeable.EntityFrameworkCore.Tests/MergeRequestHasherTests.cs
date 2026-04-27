using Granit.Mergeable;
using Granit.Mergeable.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Mergeable.EntityFrameworkCore.Tests;

public sealed class MergeRequestHasherTests
{
    private static readonly Guid Survivor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Loser = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Hash_IsStableForSameRequest()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, Reason: "manual");
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, Reason: "manual");

        MergeRequestHasher.ComputeHash(r1).ShouldBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_IsHexLowercase64Chars()
    {
        var r = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);

        string hash = MergeRequestHasher.ComputeHash(r);

        hash.Length.ShouldBe(64);
        hash.ShouldMatch("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_DiffersWhenSurvivorDiffers()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);
        var r2 = new MergeRequest(Loser, Survivor, MergeFieldChoices.Empty);

        MergeRequestHasher.ComputeHash(r1).ShouldNotBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_DiffersWhenChoicesDiffer()
    {
        var r1 = new MergeRequest(Survivor, Loser,
            MergeFieldChoices.NewBuilder().With("Name", WinnerSide.Survivor).Build());
        var r2 = new MergeRequest(Survivor, Loser,
            MergeFieldChoices.NewBuilder().With("Name", WinnerSide.Loser).Build());

        MergeRequestHasher.ComputeHash(r1).ShouldNotBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_IsStableAcrossChoicesInsertionOrder()
    {
        MergeFieldChoices a = MergeFieldChoices.NewBuilder()
            .With("Alpha", WinnerSide.Survivor)
            .With("Beta", WinnerSide.Loser)
            .Build();
        MergeFieldChoices b = MergeFieldChoices.NewBuilder()
            .With("Beta", WinnerSide.Loser)
            .With("Alpha", WinnerSide.Survivor)
            .Build();
        var r1 = new MergeRequest(Survivor, Loser, a);
        var r2 = new MergeRequest(Survivor, Loser, b);

        MergeRequestHasher.ComputeHash(r1).ShouldBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_ExcludesIdempotencyKey()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, IdempotencyKey: "key-1");
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, IdempotencyKey: "key-2");

        MergeRequestHasher.ComputeHash(r1).ShouldBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_ExcludesDryRun()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, DryRun: false);
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, DryRun: true);

        MergeRequestHasher.ComputeHash(r1).ShouldBe(MergeRequestHasher.ComputeHash(r2));
    }

    [Fact]
    public void Hash_RejectsNullRequest() =>
        Should.Throw<ArgumentNullException>(() => MergeRequestHasher.ComputeHash(null!));
}
