using Granit.Documents.Authorization;
using Shouldly;
using Xunit;

namespace Granit.Documents.EntityFrameworkCore.Tests.Authorization;

public sealed class AclCacheKeysTests
{
    [Fact]
    public void Document_KeyIsStable_ForSamePrincipal()
    {
        var docId = Guid.NewGuid();
        var principal = new DocumentPrincipal(Guid.NewGuid(), [Guid.NewGuid()], [Guid.NewGuid()]);

        string a = AclCacheKeys.Document(docId, principal);
        string b = AclCacheKeys.Document(docId, principal);

        a.ShouldBe(b);
    }

    [Fact]
    public void Document_KeyDiffers_OnGranteeSetChange()
    {
        var docId = Guid.NewGuid();
        var user = Guid.NewGuid();
        var p1 = new DocumentPrincipal(user, [Guid.NewGuid()], []);
        var p2 = new DocumentPrincipal(user, [Guid.NewGuid()], []);

        AclCacheKeys.Document(docId, p1).ShouldNotBe(AclCacheKeys.Document(docId, p2));
    }

    [Fact]
    public void HashGranteeSet_IgnoresOrder()
    {
        var r1 = Guid.NewGuid();
        var r2 = Guid.NewGuid();
        var g = Guid.NewGuid();
        var a = new DocumentPrincipal(Guid.NewGuid(), [r1, r2], [g]);
        var b = new DocumentPrincipal(a.UserId, [r2, r1], [g]);

        AclCacheKeys.HashGranteeSet(a).ShouldBe(AclCacheKeys.HashGranteeSet(b));
    }

    [Fact]
    public void HashGranteeSet_EmptyPrincipal_ReturnsZero() =>
        AclCacheKeys.HashGranteeSet(new DocumentPrincipal(Guid.Empty, [], [])).ShouldBe("0");

    [Fact]
    public void BuildEntryTags_IncludesDocFolderAndAllTags()
    {
        var docId = Guid.NewGuid();
        var f1 = Guid.NewGuid();
        var f2 = Guid.NewGuid();

        string[] tags = AclCacheKeys.BuildEntryTags(docId, [f1, f2]);

        tags.ShouldContain(AclCacheKeys.DocumentTag(docId));
        tags.ShouldContain(AclCacheKeys.FolderTag(f1));
        tags.ShouldContain(AclCacheKeys.FolderTag(f2));
        tags.ShouldContain(AclCacheKeys.AllTag);
    }
}
