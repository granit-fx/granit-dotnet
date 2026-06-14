using Granit.AI.Prompts.Domain;
using Shouldly;
using Xunit;

namespace Granit.AI.Prompts.Tests;

public sealed class PromptCategoryTests
{
    [Fact]
    public void Create_is_a_tenant_defined_non_system_category()
    {
        var category = PromptCategory.Create(Guid.NewGuid(), "Marketing");

        category.Name.ShouldBe("Marketing");
        category.IsSystem.ShouldBeFalse();
    }

    [Fact]
    public void CreateSystem_is_a_system_category()
    {
        var category = PromptCategory.CreateSystem(Guid.NewGuid(), PromptCategory.GeneralName);

        category.Name.ShouldBe("General");
        category.IsSystem.ShouldBeTrue();
    }

    [Fact]
    public void AssignCategory_adds_links_and_is_idempotent()
    {
        var prompt = PromptTemplate.Create(Guid.NewGuid(), Guid.NewGuid(), "Draft", "d", "c");
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();

        prompt.AssignCategory(Guid.NewGuid(), catA);
        prompt.AssignCategory(Guid.NewGuid(), catB);
        prompt.AssignCategory(Guid.NewGuid(), catA); // duplicate — ignored

        prompt.CategoryLinks.Select(l => l.CategoryId).ShouldBe([catA, catB], ignoreOrder: true);
    }

    [Fact]
    public void ClearCategories_removes_all_links()
    {
        var prompt = PromptTemplate.Create(Guid.NewGuid(), Guid.NewGuid(), "Draft", "d", "c");
        prompt.AssignCategory(Guid.NewGuid(), Guid.NewGuid());

        prompt.ClearCategories();

        prompt.CategoryLinks.ShouldBeEmpty();
    }
}
