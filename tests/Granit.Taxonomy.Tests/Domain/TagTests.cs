using Granit.Taxonomy.Domain;
using Granit.Taxonomy.Events;
using Shouldly;
using Xunit;

namespace Granit.Taxonomy.Tests.Domain;

public sealed class TagTests
{
    private static readonly Guid TagId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    // -------------------------------------------------------------------------
    // Create
    // -------------------------------------------------------------------------

    [Fact]
    public void Create_PopulatesAllPropertiesAndEmitsTagCreatedEvent()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");

        tag.Id.ShouldBe(TagId);
        tag.TenantId.ShouldBe(TenantId);
        tag.Scope.ShouldBe("documents");
        tag.Name.ShouldBe("urgent");
        tag.Color.ShouldBe("#FF0000");
        tag.HideOnEntityCard.ShouldBeFalse();
        tag.RowVersion.ShouldBe(1u);

        tag.DomainEvents.OfType<TagCreatedEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Create_NullTenant_AllowedForGlobalTag()
    {
        var tag = Tag.Create(TagId, tenantId: null, "global", "shared", "#0000FF");

        tag.TenantId.ShouldBeNull();
    }

    [Fact]
    public void Create_HideOnEntityCardTrue_PersistedOnAggregate()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "internal", "#888888", hideOnEntityCard: true);

        tag.HideOnEntityCard.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankScope_Throws(string scope) =>
        Should.Throw<ArgumentException>(() =>
            Tag.Create(TagId, TenantId, scope, "x", "#000000"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name) =>
        Should.Throw<ArgumentException>(() =>
            Tag.Create(TagId, TenantId, "documents", name, "#000000"));

    [Fact]
    public void Create_NameTooLong_Throws()
    {
        string tooLong = new('x', Tag.MaxNameLength + 1);
        Should.Throw<ArgumentException>(() =>
            Tag.Create(TagId, TenantId, "documents", tooLong, "#000000"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("FF0000")]      // missing '#'
    [InlineData("#GG0000")]     // non-hex digit
    [InlineData("#FF00")]       // too short
    [InlineData("#FF000000")]   // too long
    public void Create_InvalidColor_Throws(string color) =>
        Should.Throw<ArgumentException>(() =>
            Tag.Create(TagId, TenantId, "documents", "x", color));

    [Theory]
    [InlineData("#FFFFFF")]
    [InlineData("#000000")]
    [InlineData("#abcdef")]
    [InlineData("#ABCDEF")]
    [InlineData("#1a2B3c")]
    public void Create_ValidHexColors_Accepted(string color)
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "x", color);
        tag.Color.ShouldBe(color);
    }

    // -------------------------------------------------------------------------
    // Rename
    // -------------------------------------------------------------------------

    [Fact]
    public void Rename_BumpsRowVersionAndEmitsRenamedEvent()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.Rename("critical");

        tag.Name.ShouldBe("critical");
        tag.RowVersion.ShouldBe(2u);
        TagRenamedEvent ev = tag.DomainEvents.OfType<TagRenamedEvent>().ShouldHaveSingleItem();
        ev.OldName.ShouldBe("urgent");
        ev.NewName.ShouldBe("critical");
    }

    [Fact]
    public void Rename_SameName_NoOp()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.Rename("urgent");

        tag.RowVersion.ShouldBe(1u);
        tag.DomainEvents.OfType<TagRenamedEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Rename_InvalidName_Throws()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        Should.Throw<ArgumentException>(() => tag.Rename(""));
    }

    // -------------------------------------------------------------------------
    // Recolour
    // -------------------------------------------------------------------------

    [Fact]
    public void Recolour_BumpsRowVersionAndEmitsRecolouredEvent()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.Recolour("#00FF00");

        tag.Color.ShouldBe("#00FF00");
        tag.RowVersion.ShouldBe(2u);
        TagRecolouredEvent ev = tag.DomainEvents.OfType<TagRecolouredEvent>().ShouldHaveSingleItem();
        ev.OldColor.ShouldBe("#FF0000");
        ev.NewColor.ShouldBe("#00FF00");
    }

    [Fact]
    public void Recolour_SameColorIgnoringCase_NoOp()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.Recolour("#ff0000");

        tag.RowVersion.ShouldBe(1u);
        tag.DomainEvents.OfType<TagRecolouredEvent>().ShouldBeEmpty();
    }

    [Fact]
    public void Recolour_InvalidColor_Throws()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        Should.Throw<ArgumentException>(() => tag.Recolour("not-hex"));
    }

    // -------------------------------------------------------------------------
    // ToggleHideOnEntityCard
    // -------------------------------------------------------------------------

    [Fact]
    public void ToggleHideOnEntityCard_FlipsFlagAndEmitsEvent()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.ToggleHideOnEntityCard();

        tag.HideOnEntityCard.ShouldBeTrue();
        tag.RowVersion.ShouldBe(2u);
        TagHideOnEntityCardChangedEvent ev = tag.DomainEvents
            .OfType<TagHideOnEntityCardChangedEvent>().ShouldHaveSingleItem();
        ev.HideOnEntityCard.ShouldBeTrue();

        tag.ToggleHideOnEntityCard();
        tag.HideOnEntityCard.ShouldBeFalse();
        tag.RowVersion.ShouldBe(3u);
    }

    // -------------------------------------------------------------------------
    // MarkDeleted
    // -------------------------------------------------------------------------

    [Fact]
    public void MarkDeleted_EmitsDeletedEvent()
    {
        var tag = Tag.Create(TagId, TenantId, "documents", "urgent", "#FF0000");
        tag.ClearDomainEvents();

        tag.MarkDeleted();

        TagDeletedEvent ev = tag.DomainEvents.OfType<TagDeletedEvent>().ShouldHaveSingleItem();
        ev.TagId.ShouldBe(TagId);
        ev.Scope.ShouldBe("documents");
        ev.Name.ShouldBe("urgent");
    }
}
