using Granit.AI.Prompts.Domain;
using Shouldly;
using Xunit;

namespace Granit.AI.Prompts.Tests;

public sealed class PromptTemplateTests
{
    private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_stores_all_fields_as_a_user_owned_version_1_prompt()
    {
        var id = Guid.NewGuid();

        var prompt = PromptTemplate.Create(
            id, Owner, "Summarize", "Summarise the selection", "Summarise: {input}", "sparkles", "#8B5CF6");

        prompt.Id.ShouldBe(id);
        prompt.OwnerId.ShouldBe(Owner);
        prompt.Name.ShouldBe("Summarize");
        prompt.ShortDescription.ShouldBe("Summarise the selection");
        prompt.Content.ShouldBe("Summarise: {input}");
        prompt.Icon.ShouldBe("sparkles");
        prompt.IconColor!.Value.ShouldBe("#8B5CF6");
        prompt.Version.ShouldBe(1);
        prompt.IsSystem.ShouldBeFalse();
    }

    [Fact]
    public void CreateSystem_is_a_system_prompt_owned_by_no_user()
    {
        var prompt = PromptTemplate.CreateSystem(
            Guid.NewGuid(), "Daily brief", "Your day at a glance", "Give me a daily brief.");

        prompt.IsSystem.ShouldBeTrue();
        prompt.OwnerId.ShouldBe(Guid.Empty);
        prompt.Version.ShouldBe(1);
    }

    [Fact]
    public void Edit_updates_fields_and_bumps_the_version()
    {
        var prompt = PromptTemplate.Create(Guid.NewGuid(), Owner, "Draft", "Draft text", "Draft: {x}");

        prompt.Edit("Draft v2", "Draft an email", "Draft an email: {x}", "pencil", "#10B981");

        prompt.Name.ShouldBe("Draft v2");
        prompt.Content.ShouldBe("Draft an email: {x}");
        prompt.Icon.ShouldBe("pencil");
        prompt.IconColor!.Value.ShouldBe("#10B981");
        prompt.Version.ShouldBe(2);
    }

    [Theory]
    [InlineData("", "content")]
    [InlineData("name", "")]
    public void Create_rejects_blank_name_or_content(string name, string content)
    {
        Should.Throw<ArgumentException>(() => PromptTemplate.Create(Guid.NewGuid(), Owner, name, "desc", content));
    }
}
