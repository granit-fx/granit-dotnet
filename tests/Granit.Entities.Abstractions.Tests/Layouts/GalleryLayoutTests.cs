using Granit.Domain.ValueObjects;
using Granit.Entities.Layouts;
using Shouldly;
using Xunit;

namespace Granit.Entities.Abstractions.Tests.Layouts;

public sealed class GalleryLayoutTests
{
    [Fact]
    public void Descriptor_carries_image_title_subtitle_and_card_size()
    {
        EntityDefinitionDescriptor d = new SamplePhotoDefinition().Descriptor;

        d.ListLayouts.ShouldHaveSingleItem();
        GalleryLayoutDescriptor gallery = d.ListLayouts.Single().ShouldBeOfType<GalleryLayoutDescriptor>();

        gallery.Kind.ShouldBe(EntityListLayoutKind.Gallery);
        gallery.ImagePropertyName.ShouldBe("Thumbnail");
        gallery.TitlePropertyName.ShouldBe("Caption");
        gallery.SubtitlePropertyName.ShouldBe("Author");
        gallery.CardSize.ShouldBe(GalleryCardSize.Large);
    }

    [Fact]
    public void IsDefault_and_RequiresPermission_round_trip()
    {
        EntityDefinitionDescriptor d = new SamplePhotoDefinition().Descriptor;
        EntityListLayoutDescriptor gallery = d.ListLayouts.Single();

        gallery.IsDefault.ShouldBeTrue();
        gallery.RequiresPermission.ShouldBe("Photos.Photos.Gallery");
    }

    [Fact]
    public void Title_subtitle_are_optional_and_card_size_defaults_to_medium()
    {
        EntityDefinitionDescriptor d = new MinimalGalleryDefinition().Descriptor;
        var gallery = (GalleryLayoutDescriptor)d.ListLayouts.Single();

        gallery.ImagePropertyName.ShouldBe("Thumbnail");
        gallery.TitlePropertyName.ShouldBeNull();
        gallery.SubtitlePropertyName.ShouldBeNull();
        gallery.CardSize.ShouldBe(GalleryCardSize.Medium);
    }

    [Fact]
    public void Build_rejects_missing_image_field()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new MissingImageFieldDefinition().Descriptor);

        ex.Message.ShouldContain("ImageField");
    }

    [Fact]
    public void Build_rejects_non_property_image_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadImageLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("ImageField selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_non_property_title_lambda()
    {
        ArgumentException ex = Should.Throw<ArgumentException>(() =>
            _ = new BadTitleLambdaDefinition().Descriptor);

        ex.Message.ShouldContain("TitleField selector must be a direct property access");
    }

    [Fact]
    public void Build_rejects_two_default_layouts()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new TwoDefaultsDefinition().Descriptor);

        ex.Message.ShouldContain("At most one list-view layout may be marked IsDefault");
    }

    [Fact]
    public void Build_rejects_duplicate_gallery_kinds()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            _ = new DuplicateGalleriesDefinition().Descriptor);

        ex.Message.ShouldContain("Duplicate list-view layout kind 'Gallery'");
    }

    private sealed class SamplePhoto
    {
        public BlobReference? Thumbnail { get; set; }
        public string Caption { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public DateTimeOffset CapturedAt { get; set; }
    }

    private sealed class SamplePhotoDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.Photo";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder.GalleryView(g => g
                .IsDefault()
                .RequiresPermission("Photos.Photos.Gallery")
                .ImageField(p => p.Thumbnail)
                .TitleField(p => p.Caption)
                .SubtitleField(p => p.Author)
                .CardSize(GalleryCardSize.Large));
    }

    private sealed class MinimalGalleryDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.MinimalGallery";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder.GalleryView(g => g.ImageField(p => p.Thumbnail));
    }

    private sealed class MissingImageFieldDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.MissingImage";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder.GalleryView(g => g.TitleField(p => p.Caption));
    }

    private sealed class BadImageLambdaDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.BadImageLambda";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder.GalleryView(g => g.ImageField(p => p.Thumbnail == null ? null : p.Thumbnail));
    }

    private sealed class BadTitleLambdaDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.BadTitleLambda";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder.GalleryView(g => g
                .ImageField(p => p.Thumbnail)
                .TitleField(p => p.Caption.ToUpperInvariant()));
    }

    private sealed class TwoDefaultsDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.TwoGalleryDefaults";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder
                .GalleryView(g => g.IsDefault().ImageField(p => p.Thumbnail))
                .CalendarView(c => c.IsDefault().StartField(p => p.CapturedAt));
    }

    private sealed class DuplicateGalleriesDefinition : EntityDefinition<SamplePhoto>
    {
        public override string Name => "Granit.Sample.DuplicateGalleries";
        protected override void Configure(EntityDefinitionBuilder<SamplePhoto> builder) =>
            builder
                .GalleryView(g => g.ImageField(p => p.Thumbnail))
                .GalleryView(g => g.ImageField(p => p.Thumbnail));
    }
}
