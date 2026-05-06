using Granit.Entities;
using Granit.Entities.Customization.Domain;
using Granit.Entities.Customization.Domain.Deltas;
using Granit.Entities.Customization.Internal;
using Granit.Entities.Manifests;
using Granit.MultiTenancy;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Entities.Customization.Tests.Internal;

public sealed class EntityCustomizationManifestApplierTests
{
    private const string EntityName = "Granit.Parties.Party";

    [Fact]
    public async Task No_customization_returns_input_unchanged()
    {
        EntityManifestResponse manifest = ManifestWith(BuildSection("identity", "FirstName", "LastName"));
        EntityCustomizationManifestApplier sut = SutFor(customization: null);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(manifest);
    }

    [Fact]
    public async Task Empty_delta_list_returns_input_unchanged()
    {
        EntityManifestResponse manifest = ManifestWith(BuildSection("identity", "FirstName", "LastName"));
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: []);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(manifest);
    }

    [Fact]
    public async Task Hide_drops_field_and_records_HiddenByOverride()
    {
        EntityManifestResponse manifest = ManifestWith(BuildSection("identity", "FirstName", "LastName"));
        var overrideId = Guid.NewGuid();
        var customization = EntityCustomization.Create(
            id: overrideId,
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new HideDelta("LastName")]);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        EntityFormManifest form = result.Forms!.ShouldHaveSingleItem();
        EntityFormSectionManifest section = form.Sections.ShouldHaveSingleItem();
        section.Fields.Select(f => f.PropertyName).ShouldBe(["FirstName"]);
        form.HiddenByOverride.ShouldNotBeNull();
        form.HiddenByOverride.ShouldBe(["LastName"]);
    }

    [Fact]
    public async Task Section_with_all_fields_hidden_is_dropped()
    {
        EntityManifestResponse manifest = ManifestWith(BuildSection("identity", "FirstName"));
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new HideDelta("FirstName")]);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        result.Forms!.ShouldHaveSingleItem().Sections.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reorder_with_Before_anchor_repositions_within_section()
    {
        EntityManifestResponse manifest = ManifestWith(
            BuildSection("identity", "FirstName", "LastName", "Email"));
        var overrideId = Guid.NewGuid();
        var customization = EntityCustomization.Create(
            id: overrideId,
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new ReorderDelta("Email", BeforeFieldName: "FirstName", AfterFieldName: null)]);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        EntityFormSectionManifest section = result.Forms!.ShouldHaveSingleItem().Sections.ShouldHaveSingleItem();
        section.Fields.Select(f => f.PropertyName).ShouldBe(["Email", "FirstName", "LastName"]);
        EntityFormFieldManifest moved = section.Fields[0];
        moved.Provenance.ShouldNotBeNull();
        moved.Provenance.Layer.ShouldBe(EntityProvenanceLayers.TenantCustomization);
        moved.Provenance.OverrideId.ShouldBe(overrideId);
    }

    [Fact]
    public async Task Regroup_moves_field_across_sections_with_provenance()
    {
        EntityManifestResponse manifest = ManifestWith(
            BuildSection("identity", "FirstName"),
            BuildSection("contact", "Email"));
        var overrideId = Guid.NewGuid();
        var customization = EntityCustomization.Create(
            id: overrideId,
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new RegroupDelta("Email", "identity")]);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        IReadOnlyList<EntityFormSectionManifest> sections = result.Forms!.ShouldHaveSingleItem().Sections;
        EntityFormSectionManifest identitySection = sections.First(s => s.Key == "identity");
        identitySection.Fields.Select(f => f.PropertyName).ShouldBe(["FirstName", "Email"]);
        EntityFormFieldManifest moved = identitySection.Fields[1];
        moved.Provenance!.Layer.ShouldBe(EntityProvenanceLayers.TenantCustomization);
        moved.Provenance.OverrideId.ShouldBe(overrideId);
        // contact is now empty — section is dropped per the same rule that drops sections
        // emptied by Hide deltas (no surviving fields → drop the empty header).
        sections.ShouldNotContain(s => s.Key == "contact");
    }

    [Fact]
    public async Task Dangling_field_reference_is_silently_dropped()
    {
        EntityManifestResponse manifest = ManifestWith(BuildSection("identity", "FirstName"));
        var customization = EntityCustomization.Create(
            id: Guid.NewGuid(),
            entityName: EntityName,
            layoutKind: LayoutKind.FormDefault,
            deltas: [new HideDelta("Phantom")]);
        EntityCustomizationManifestApplier sut = SutFor(customization);

        EntityManifestResponse result = await sut.ApplyAsync(EntityName, manifest, TestContext.Current.CancellationToken);

        EntityFormManifest form = result.Forms!.ShouldHaveSingleItem();
        form.HiddenByOverride.ShouldBeNull();
        form.Sections.ShouldHaveSingleItem().Fields.Select(f => f.PropertyName).ShouldBe(["FirstName"]);
    }

    private static EntityCustomizationManifestApplier SutFor(EntityCustomization? customization)
    {
        IEntityCustomizationReader reader = Substitute.For<IEntityCustomizationReader>();
        reader.GetAsync(EntityName, LayoutKind.FormDefault, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(customization);

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(true);
        tenant.Id.Returns(Guid.NewGuid());

        return new EntityCustomizationManifestApplier(reader, tenant);
    }

    private static EntityManifestResponse ManifestWith(params EntityFormSectionManifest[] sections) =>
        new(
            SchemaVersion: 2,
            Identity: null,
            Permissions: null,
            Forms: [new EntityFormManifest("default", Customizable: true, sections)],
            Details: null,
            Collections: null,
            Relations: null,
            Actions: null,
            Activities: null);

    private static EntityFormSectionManifest BuildSection(string key, params string[] fieldNames) =>
        new(
            Key: key,
            LabelKey: null,
            Order: 0,
            CollapsedByDefault: false,
            Fields: [.. fieldNames.Select(n => new EntityFormFieldManifest(
                PropertyName: n,
                ClrTypeName: "String",
                Component: "text",
                Config: null,
                LabelKey: null,
                HelpKey: null,
                Order: 0,
                ReadOnly: false,
                VisibleIf: null))]);
}
