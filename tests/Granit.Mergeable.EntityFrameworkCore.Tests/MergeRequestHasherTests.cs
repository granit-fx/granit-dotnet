using Granit.Mergeable.EntityFrameworkCore.Internal;
using Shouldly;
using Xunit;

namespace Granit.Mergeable.EntityFrameworkCore.Tests;

public sealed class MergeRequestHasherTests
{
    private static readonly Guid Survivor = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Loser = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // Synthetic test key — the production HMAC key is derived from IStringEncryptionService.
    // We verify only the deterministic / stability properties of the keyed hash here.
    private static readonly byte[] MacKey = new byte[32]
    {
        1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
        17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32,
    };

    [Fact]
    public void Hash_IsStableForSameRequest()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, Reason: "manual");
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, Reason: "manual");

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
    }

    [Fact]
    public void Hash_IsHexLowercase64Chars()
    {
        var r = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);

        string hash = MergeRequestHasher.ComputeHash(r, tenantId: null, MacKey);

        hash.Length.ShouldBe(64);
        hash.ShouldMatch("^[0-9a-f]+$");
    }

    [Fact]
    public void Hash_DiffersWhenSurvivorDiffers()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);
        var r2 = new MergeRequest(Loser, Survivor, MergeFieldChoices.Empty);

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldNotBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
    }

    [Fact]
    public void Hash_DiffersWhenChoicesDiffer()
    {
        var r1 = new MergeRequest(Survivor, Loser,
            MergeFieldChoices.NewBuilder().With("Name", WinnerSide.Survivor).Build());
        var r2 = new MergeRequest(Survivor, Loser,
            MergeFieldChoices.NewBuilder().With("Name", WinnerSide.Loser).Build());

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldNotBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
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

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
    }

    [Fact]
    public void Hash_ExcludesIdempotencyKey()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, IdempotencyKey: "key-1");
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, IdempotencyKey: "key-2");

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
    }

    [Fact]
    public void Hash_ExcludesDryRun()
    {
        var r1 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, DryRun: false);
        var r2 = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty, DryRun: true);

        MergeRequestHasher.ComputeHash(r1, TenantA, MacKey)
            .ShouldBe(MergeRequestHasher.ComputeHash(r2, TenantA, MacKey));
    }

    [Fact]
    public void Hash_DiffersAcrossTenants()
    {
        // Cross-tenant key oracle defence: same canonical body in two different tenants must
        // yield distinct hashes so the (TenantId, Key, RequestHash) lookup is partitioned.
        var r = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);

        MergeRequestHasher.ComputeHash(r, TenantA, MacKey)
            .ShouldNotBe(MergeRequestHasher.ComputeHash(r, TenantB, MacKey));
    }

    [Fact]
    public void Hash_DiffersAcrossMacKeys()
    {
        // Defence: a different MAC key produces a different digest. An attacker
        // who lacks the deployment key cannot pre-compute a matching RequestHash for a
        // future legitimate body.
        byte[] otherKey = new byte[32];
        Array.Fill<byte>(otherKey, 0xFF);

        var r = new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty);

        MergeRequestHasher.ComputeHash(r, TenantA, MacKey)
            .ShouldNotBe(MergeRequestHasher.ComputeHash(r, TenantA, otherKey));
    }

    [Fact]
    public void Hash_RejectsNullRequest() =>
        Should.Throw<ArgumentNullException>(() => MergeRequestHasher.ComputeHash(null!, TenantA, MacKey));

    [Fact]
    public void Hash_RejectsNullKey() =>
        Should.Throw<ArgumentNullException>(() => MergeRequestHasher.ComputeHash(
            new MergeRequest(Survivor, Loser, MergeFieldChoices.Empty), TenantA, null!));
}
