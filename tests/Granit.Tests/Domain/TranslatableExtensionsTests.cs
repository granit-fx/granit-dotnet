using Granit.Domain;
using Shouldly;
using Xunit;

namespace Granit.Tests.Domain;

public sealed class TranslatableExtensionsTests
{
    // -------------------------------------------------------------------------
    // Test entities
    // -------------------------------------------------------------------------

    private sealed class TestParent : Entity, ITranslatable<TestTranslation>
    {
        public ICollection<TestTranslation> Translations { get; set; } = [];
    }

    private sealed class TestTranslation : Translation<TestParent>
    {
        public string Title { get; set; } = string.Empty;
    }

    private sealed class TestAuditedParent : AuditedEntity, ITranslatable<TestAuditedTranslation>
    {
        public ICollection<TestAuditedTranslation> Translations { get; set; } = [];
    }

    private sealed class TestAuditedTranslation : AuditedTranslation<TestAuditedParent>
    {
        public string Title { get; set; } = string.Empty;
    }

    // -------------------------------------------------------------------------
    // Hierarchy
    // -------------------------------------------------------------------------

    [Fact]
    public void Translation_InheritsFromEntity() =>
        new TestTranslation().ShouldBeAssignableTo<Entity>();

    [Fact]
    public void Translation_ImplementsITranslation() =>
        new TestTranslation().ShouldBeAssignableTo<ITranslation>();

    [Fact]
    public void Translation_ImplementsITranslationOfParent() =>
        new TestTranslation().ShouldBeAssignableTo<ITranslation<TestParent>>();

    [Fact]
    public void AuditedTranslation_InheritsFromAuditedEntity() =>
        new TestAuditedTranslation().ShouldBeAssignableTo<AuditedEntity>();

    [Fact]
    public void AuditedTranslation_ImplementsITranslation() =>
        new TestAuditedTranslation().ShouldBeAssignableTo<ITranslation>();

    [Fact]
    public void AuditedTranslation_ImplementsITranslationOfParent() =>
        new TestAuditedTranslation().ShouldBeAssignableTo<ITranslation<TestAuditedParent>>();

    // -------------------------------------------------------------------------
    // Default values
    // -------------------------------------------------------------------------

    [Fact]
    public void Translation_DefaultValues_AreCorrect()
    {
        TestTranslation translation = new();

        translation.Id.ShouldBe(Guid.Empty);
        translation.ParentId.ShouldBe(Guid.Empty);
        translation.Culture.ShouldBeEmpty();
        translation.Parent.ShouldBeNull();
    }

    [Fact]
    public void AuditedTranslation_DefaultValues_IncludeAuditFields()
    {
        TestAuditedTranslation translation = new();

        translation.Id.ShouldBe(Guid.Empty);
        translation.ParentId.ShouldBe(Guid.Empty);
        translation.Culture.ShouldBeEmpty();
        translation.CreatedAt.ShouldBe(default);
        translation.CreatedBy.ShouldBeEmpty();
        translation.ModifiedAt.ShouldBeNull();
        translation.ModifiedBy.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GetTranslation — exact match
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_ExactCultureMatch_ReturnsExact()
    {
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("fr");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("fr");
    }

    [Fact]
    public void GetTranslation_ExactCultureMatch_CaseInsensitive()
    {
        TestParent entity = CreateEntityWithTranslations("fr-BE", "en");

        TestTranslation? result = entity.GetTranslation("FR-BE");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("fr-BE");
    }

    // -------------------------------------------------------------------------
    // GetTranslation — parent culture fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_ParentCultureFallback_ReturnsFr()
    {
        // fr-BE demandé, seul fr disponible → fallback vers fr
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("fr-BE");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("fr");
    }

    // -------------------------------------------------------------------------
    // GetTranslation — default culture fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_DefaultCultureFallback_ReturnsEn()
    {
        // de demandé, pas de de ni parent, fallback vers en (défaut)
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("de");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("en");
    }

    [Fact]
    public void GetTranslation_CustomDefaultCulture_ReturnsFr()
    {
        TestParent entity = CreateEntityWithTranslations("fr", "nl");

        TestTranslation? result = entity.GetTranslation("de", defaultCulture: "fr");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("fr");
    }

    // -------------------------------------------------------------------------
    // GetTranslation — first available fallback
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_NoDefaultCulture_ReturnsFirstAvailable()
    {
        // de demandé, pas de de ni en, retourne la première disponible
        TestParent entity = CreateEntityWithTranslations("fr", "nl");

        TestTranslation? result = entity.GetTranslation("de");

        result.ShouldNotBeNull();
        new[] { "fr", "nl" }.ShouldContain(result!.Culture);
    }

    // -------------------------------------------------------------------------
    // GetTranslation — empty collection
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_EmptyCollection_ReturnsNull()
    {
        TestParent entity = new();

        TestTranslation? result = entity.GetTranslation("fr");

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GetTranslation — strict mode (useFallback: false)
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_Strict_ExactMatch_ReturnsExact()
    {
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("fr", useFallback: false);

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("fr");
    }

    [Fact]
    public void GetTranslation_Strict_NoMatch_ReturnsNull()
    {
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("de", useFallback: false);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetTranslation_Strict_ParentCultureExists_ReturnsNull()
    {
        // fr-BE demandé en mode strict, seul fr disponible → pas de fallback
        TestParent entity = CreateEntityWithTranslations("fr", "en");

        TestTranslation? result = entity.GetTranslation("fr-BE", useFallback: false);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetTranslation_Strict_EmptyCollection_ReturnsNull()
    {
        TestParent entity = new();

        TestTranslation? result = entity.GetTranslation("fr", useFallback: false);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GetTranslation — same culture as default does not cause double lookup
    // -------------------------------------------------------------------------

    [Fact]
    public void GetTranslation_CultureSameAsDefault_ReturnsCorrectly()
    {
        TestParent entity = CreateEntityWithTranslations("en");

        TestTranslation? result = entity.GetTranslation("en");

        result.ShouldNotBeNull();
        result!.Culture.ShouldBe("en");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static TestParent CreateEntityWithTranslations(params string[] cultures)
    {
        TestParent entity = new() { Id = Guid.NewGuid() };

        foreach (string culture in cultures)
        {
            entity.Translations.Add(new TestTranslation
            {
                Id = Guid.NewGuid(),
                ParentId = entity.Id,
                Culture = culture,
                Title = $"Title in {culture}",
            });
        }

        return entity;
    }
}
