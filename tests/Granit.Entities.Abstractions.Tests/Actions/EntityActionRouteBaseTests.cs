using Granit.Entities.Actions;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Actions;

public sealed class EntityActionRouteBaseTests
{
    [Fact]
    public void Post_with_route_base_no_path_uses_action_name_segment()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor archive = d.Actions.Single(a => a.Name == "archive");
        archive.Kind.ShouldBe(EntityActionKind.ApiCall);
        archive.HttpMethod.ShouldBe("POST");
        archive.UrlTemplate.ShouldBe("/api/parties/{id}/archive");
    }

    [Fact]
    public void Post_with_route_base_explicit_path_uses_provided_segment()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor custom = d.Actions.Single(a => a.Name == "custom");
        custom.HttpMethod.ShouldBe("POST");
        custom.UrlTemplate.ShouldBe("/api/parties/{id}/custom-segment");
    }

    [Fact]
    public void Put_delete_patch_emit_correct_methods()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        d.Actions.Single(a => a.Name == "rename").HttpMethod.ShouldBe("PUT");
        d.Actions.Single(a => a.Name == "remove").HttpMethod.ShouldBe("DELETE");
        d.Actions.Single(a => a.Name == "patch-status").HttpMethod.ShouldBe("PATCH");
    }

    [Fact]
    public void Get_uses_apicall_kind_with_GET_method()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor stats = d.Actions.Single(a => a.Name == "stats");
        stats.Kind.ShouldBe(EntityActionKind.ApiCall);
        stats.HttpMethod.ShouldBe("GET");
        stats.UrlTemplate.ShouldBe("/api/parties/{id}/stats");
    }

    [Fact]
    public void Download_with_route_base_uses_download_kind_and_no_method()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor export = d.Actions.Single(a => a.Name == "export");
        export.Kind.ShouldBe(EntityActionKind.Download);
        export.HttpMethod.ShouldBeNull();
        export.UrlTemplate.ShouldBe("/api/parties/{id}/export");
    }

    [Fact]
    public void OnListHeader_with_verb_shortcut_omits_id_segment()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor import = d.Actions.Single(a => a.Name == "import");
        import.HttpMethod.ShouldBe("POST");
        import.ShowOnListHeader.ShouldBeTrue();
        import.UrlTemplate.ShouldBe("/api/parties/import");
    }

    [Fact]
    public void Navigate_does_not_compose_from_route_base()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor merge = d.Actions.Single(a => a.Name == "merge");
        merge.Kind.ShouldBe(EntityActionKind.Navigate);
        merge.UrlTemplate.ShouldBe("/w/parties/{id}/merge");
    }

    [Fact]
    public void AbsolutePath_overrides_route_base_composition()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor offsite = d.Actions.Single(a => a.Name == "offsite");
        offsite.Kind.ShouldBe(EntityActionKind.ApiCall);
        offsite.HttpMethod.ShouldBe("POST");
        offsite.UrlTemplate.ShouldBe("/legacy/offsite/path");
    }

    [Fact]
    public void ApiCall_full_url_is_unaffected_by_route_base()
    {
        EntityDefinitionDescriptor d = new RouteBaseDefinition().Descriptor;

        EntityActionDescriptor legacy = d.Actions.Single(a => a.Name == "legacy");
        legacy.HttpMethod.ShouldBe("POST");
        legacy.UrlTemplate.ShouldBe("/raw/url/{id}");
    }

    [Fact]
    public void Verb_shortcut_without_route_base_throws_clear_error()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new MissingRouteBaseDefinition().Descriptor);

        ex.Message.ShouldContain("RouteBase");
        ex.Message.ShouldContain("orphan");
    }

    [Fact]
    public void Download_legacy_full_url_still_works_without_route_base()
    {
        EntityDefinitionDescriptor d = new LegacyDownloadDefinition().Descriptor;

        EntityActionDescriptor pdf = d.Actions.Single(a => a.Name == "download-pdf");
        pdf.Kind.ShouldBe(EntityActionKind.Download);
        pdf.UrlTemplate.ShouldBe("/api/v1/orders/{id}/pdf");
    }

    [Fact]
    public void Route_base_trims_trailing_slash()
    {
        EntityDefinitionDescriptor d = new TrailingSlashRouteBaseDefinition().Descriptor;

        d.Actions.Single(a => a.Name == "archive").UrlTemplate.ShouldBe("/api/parties/{id}/archive");
    }

    private sealed class SampleEntity;

    private sealed class RouteBaseDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.RouteBase";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder
                .RouteBase("/api/parties")
                .Action("archive", a => a.Post().Order(10))
                .Action("custom", a => a.Post("custom-segment").Order(20))
                .Action("rename", a => a.Put().Order(30))
                .Action("remove", a => a.Delete().Order(40))
                .Action("patch-status", a => a.Patch().Order(50))
                .Action("stats", a => a.Get().Order(60))
                .Action("export", a => a.Download().Order(70))
                .Action("import", a => a.Post().OnListHeader().Order(80))
                .Action("merge", a => a.Navigate("/w/parties/{id}/merge").Order(90))
                .Action("offsite", a => a.Post().AbsolutePath("/legacy/offsite/path").Order(100))
                .Action("legacy", a => a.ApiCall("POST", "/raw/url/{id}").Order(110));
    }

    private sealed class MissingRouteBaseDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.OrphanShortcut";

        // Sentinel name "orphan" appears in the error message so the test can
        // assert on the diagnostic text without rebinding to the verb-shortcut
        // wording, which may evolve.
        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder.Action("orphan", a => a.Post());
    }

    private sealed class LegacyDownloadDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.LegacyDownload";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder.Action("download-pdf", a => a
                .Download("/api/v1/orders/{id}/pdf")
                .DisplayKey("Orders:Action.Pdf")
                .Icon("file-down"));
    }

    private sealed class TrailingSlashRouteBaseDefinition : EntityDefinition<SampleEntity>
    {
        public override string Name => "Granit.Sample.TrailingSlash";

        protected override void Configure(EntityDefinitionBuilder<SampleEntity> builder) =>
            builder
                .RouteBase("/api/parties/")
                .Action("archive", a => a.Post());
    }
}
