using Granit.Templating.Keys;
using Shouldly;
using Xunit;

namespace Granit.Templating.Tests.Keys;

public sealed class TemplateKeyTests
{
    [Fact]
    public void Constructor_WithNameOnly_CultureIsNull()
    {
        TemplateKey key = new("Billing.Invoice");

        key.Name.ShouldBe("Billing.Invoice");
        key.Culture.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithNameAndCulture_BothPropertiesSet()
    {
        TemplateKey key = new("Billing.Invoice", "fr-BE");

        key.Name.ShouldBe("Billing.Invoice");
        key.Culture.ShouldBe("fr-BE");
    }

    [Fact]
    public void Equality_SameNameAndCulture_AreEqual()
    {
        TemplateKey key1 = new("Billing.Invoice", "fr");
        TemplateKey key2 = new("Billing.Invoice", "fr");

        key1.ShouldBe(key2);
    }

    [Fact]
    public void Equality_DifferentCulture_AreNotEqual()
    {
        TemplateKey key1 = new("Billing.Invoice", "fr");
        TemplateKey key2 = new("Billing.Invoice", "en");

        key1.ShouldNotBe(key2);
    }

    [Fact]
    public void Equality_NullCultureVsSpecificCulture_AreNotEqual()
    {
        TemplateKey neutral = new("Billing.Invoice");
        TemplateKey specific = new("Billing.Invoice", "fr");

        neutral.ShouldNotBe(specific);
    }

    [Fact]
    public void Equality_BothNullCulture_AreEqual()
    {
        TemplateKey key1 = new("Billing.Invoice");
        TemplateKey key2 = new("Billing.Invoice");

        key1.ShouldBe(key2);
    }

    [Fact]
    public void GetHashCode_EqualKeys_SameHashCode()
    {
        TemplateKey key1 = new("Billing.Invoice", "fr");
        TemplateKey key2 = new("Billing.Invoice", "fr");

        key1.GetHashCode().ShouldBe(key2.GetHashCode());
    }

    [Fact]
    public void ToString_ContainsNameAndCulture()
    {
        TemplateKey key = new("Billing.Invoice", "fr-BE");

        string result = key.ToString();

        result.ShouldContain("Billing.Invoice");
        result.ShouldContain("fr-BE");
    }

    [Fact]
    public void With_ChangeCulture_ReturnsNewKeyWithUpdatedCulture()
    {
        TemplateKey original = new("Billing.Invoice", "fr");
        TemplateKey modified = original with { Culture = "en" };

        modified.Name.ShouldBe("Billing.Invoice");
        modified.Culture.ShouldBe("en");
        original.Culture.ShouldBe("fr");
    }
}
