using Granit.AI.Prompts.Domain;
using Granit.AI.Prompts.Endpoints.Dtos;
using Granit.AI.Prompts.Endpoints.Internal;
using Granit.AI.Prompts.Endpoints.Permissions;
using Granit.AI.Prompts.Endpoints.Validators;
using Granit.Authorization;
using Granit.Localization;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace Granit.AI.Prompts.Endpoints.Tests;

public sealed class PromptCatalogueEndpointsUnitTests
{
    private sealed class CapturingPermissionContext : IPermissionDefinitionContext
    {
        public List<PermissionGroup> Groups { get; } = [];

        public PermissionGroup AddGroup(string name, LocalizableString? displayName = null)
        {
            PermissionGroup group = new(name, displayName);
            Groups.Add(group);
            return group;
        }
    }

    /// <summary>A localizer that echoes a known prefix for resolved keys and reports the rest as missing.</summary>
    private sealed class StubLocalizer : IStringLocalizer<AIPromptsLocalizationResource>
    {
        public LocalizedString this[string name] =>
            name.StartsWith("Prompt:", StringComparison.Ordinal)
                ? new LocalizedString(name, $"resolved::{name}", resourceNotFound: false)
                : new LocalizedString(name, name, resourceNotFound: true);

        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    [Fact]
    public void Permission_provider_declares_read_manage_delete()
    {
        CapturingPermissionContext context = new();

        new AIPromptsPermissionDefinitionProvider().DefinePermissions(context);

        PermissionGroup group = context.Groups.ShouldHaveSingleItem();
        group.Name.ShouldBe("AIPrompts");
        group.Permissions.Select(p => p.Name).ShouldBe(
            [
                "AIPrompts.Templates.Read",
                "AIPrompts.Templates.Manage",
                "AIPrompts.Templates.Delete",
            ],
            ignoreOrder: true);
    }

    [Fact]
    public void Create_validator_rejects_blank_overlong_and_bad_colour()
    {
        CreatePromptRequestValidator validator = new();

        validator.Validate(new CreatePromptRequest("", "content")).IsValid.ShouldBeFalse();
        validator.Validate(new CreatePromptRequest("Name", "")).IsValid.ShouldBeFalse();
        validator.Validate(new CreatePromptRequest(new string('x', 201), "content")).IsValid.ShouldBeFalse();
        validator.Validate(new CreatePromptRequest("Name", "content", IconColor: "not-a-colour")).IsValid.ShouldBeFalse();

        var tooMany = Enumerable.Range(0, CreatePromptRequestValidator.MaxCategories + 1).Select(_ => Guid.NewGuid()).ToList();
        validator.Validate(new CreatePromptRequest("Name", "content", CategoryIds: tooMany)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Create_validator_accepts_a_well_formed_request()
    {
        CreatePromptRequestValidator validator = new();

        var request = new CreatePromptRequest(
            "Summary", "Summarise this", ShortDescription: "A summary", Icon: "sparkles",
            IconColor: "#8B5CF6", CategoryIds: [Guid.NewGuid()]);

        validator.Validate(request).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Update_validator_rejects_blank_name_and_accepts_valid()
    {
        UpdatePromptRequestValidator validator = new();

        validator.Validate(new UpdatePromptRequest("  ", "content")).IsValid.ShouldBeFalse();
        validator.Validate(new UpdatePromptRequest("Name", "content", IconColor: "#FFF")).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Response_projects_the_aggregate_with_categories_and_colour()
    {
        var ownerId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var prompt = PromptTemplate.Create(Guid.NewGuid(), ownerId, "Draft", "desc", "Draft: {x}", "pencil", "#10B981");
        prompt.AssignCategory(Guid.NewGuid(), categoryId);

        var response = PromptResponse.FromAggregate(prompt, prompt.Name, prompt.ShortDescription);

        response.Name.ShouldBe("Draft");
        response.IconColor.ShouldBe("#10B981");
        response.OwnerId.ShouldBe(ownerId);
        response.IsSystem.ShouldBeFalse();
        response.CategoryIds.ShouldBe([categoryId]);
    }

    [Fact]
    public void Display_resolves_system_keys_but_keeps_user_text_literal()
    {
        StubLocalizer localizer = new();

        var system = PromptTemplate.CreateSystem(Guid.NewGuid(), "Prompt:Summarize:Name", "Prompt:Summarize:Description", "content");
        (string sysName, string sysDescription) = PromptDisplay.Resolve(localizer, system);
        sysName.ShouldBe("resolved::Prompt:Summarize:Name");
        sysDescription.ShouldBe("resolved::Prompt:Summarize:Description");

        var user = PromptTemplate.Create(Guid.NewGuid(), Guid.NewGuid(), "My prompt", "My description", "content");
        (string userName, string userDescription) = PromptDisplay.Resolve(localizer, user);
        userName.ShouldBe("My prompt");
        userDescription.ShouldBe("My description");
    }
}
