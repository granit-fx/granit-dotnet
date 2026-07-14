using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Mapping.Internal;
using Granit.DataExchange.Import.Parsing;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Mapping;

public sealed class CompiledDataMapperTests
{
    private static readonly ImportOptions Options = new();

    private static CompiledDataMapper<SampleEntity> CreateMapper() =>
        new(new SampleImportDefinition());

    private static RawImportRow Row(params (string Column, string? Value)[] cells) =>
        new(1, cells.ToDictionary(c => c.Column, c => c.Value, StringComparer.Ordinal));

    private static IReadOnlyList<ImportColumnMapping> Mappings(params string[] targets) =>
        [.. targets.Select(t => new ImportColumnMapping(t, t, MappingConfidence.Manual))];

    private static async Task<MappingResult<SampleEntity>> MapSingleAsync(string target, string? raw)
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();
        return await mapper.MapAsync(Row((target, raw)), Mappings(target), Options,
            TestContext.Current.CancellationToken);
    }

    // ── Valid conversions ────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_converts_every_supported_type()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();
        RawImportRow row = Row(
            ("Text", "hello"),
            ("Flag", "true"),
            ("Uid", "0f8fad5b-d9cb-469f-a165-70867728950e"),
            ("Color", "Green"),
            ("MaybeColor", "blue"),
            ("Count", "42"),
            ("BigCount", "9000000000"),
            ("SmallCount", "-12"),
            ("TinyCount", "200"),
            ("Price", "19.99"),
            ("Ratio", "0.5"),
            ("Score", "1.25"),
            ("Timestamp", "2026-01-15T08:30:00"),
            ("Offset", "2026-01-15T08:30:00+02:00"),
            ("Day", "2026-01-15"),
            ("Time", "08:30:00"),
            ("MaybeCount", "7"),
            ("FormattedDate", "15/01/2026"));

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            row,
            Mappings("Text", "Flag", "Uid", "Color", "MaybeColor", "Count", "BigCount", "SmallCount",
                "TinyCount", "Price", "Ratio", "Score", "Timestamp", "Offset", "Day", "Time",
                "MaybeCount", "FormattedDate"),
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors.Select(e => $"{e.TargetProperty}:{e.ErrorCode}")));
        SampleEntity entity = result.Entity!;
        entity.Text.ShouldBe("hello");
        entity.Flag.ShouldBeTrue();
        entity.Uid.ShouldBe(Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e"));
        entity.Color.ShouldBe(SampleColor.Green);
        entity.MaybeColor.ShouldBe(SampleColor.Blue); // enum parsing is case-insensitive
        entity.Count.ShouldBe(42);
        entity.BigCount.ShouldBe(9_000_000_000L);
        entity.SmallCount.ShouldBe((short)-12);
        entity.TinyCount.ShouldBe((byte)200);
        entity.Price.ShouldBe(19.99m);
        entity.Ratio.ShouldBe(0.5);
        entity.Score.ShouldBe(1.25f);
        entity.Timestamp.ShouldBe(new DateTime(2026, 1, 15, 8, 30, 0));
        entity.Offset.ShouldBe(new DateTimeOffset(2026, 1, 15, 8, 30, 0, TimeSpan.FromHours(2)));
        entity.Day.ShouldBe(new DateOnly(2026, 1, 15));
        entity.Time.ShouldBe(new TimeOnly(8, 30, 0));
        entity.MaybeCount.ShouldBe(7);
        entity.FormattedDate.ShouldBe(new DateTime(2026, 1, 15)); // Format-driven (dd/MM/yyyy)
    }

    // ── Invalid / out-of-range matrix — exact error codes, never throws ─────

    [Theory]
    [InlineData("Flag", "notabool", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Uid", "not-a-guid", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Color", "chartreuse", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Color", "999", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("MaybeColor", "chartreuse", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Count", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Count", "1.5", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Count", "9999999999", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("BigCount", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("BigCount", "99999999999999999999", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("SmallCount", "40000", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("SmallCount", "xyz", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("TinyCount", "300", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("TinyCount", "-1", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("Price", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Price", "1e300", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("Ratio", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Ratio", "1e400", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("Score", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Score", "1e40", CompiledDataMapper<SampleEntity>.OverflowErrorCode)]
    [InlineData("Timestamp", "not-a-date", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Offset", "not-a-date", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Day", "2026-45-99", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("Time", "27:99", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("MaybeCount", "abc", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    [InlineData("FormattedDate", "2026-01-15", CompiledDataMapper<SampleEntity>.InvalidFormatErrorCode)]
    public async Task MapAsync_bad_value_yields_exact_error_code(string target, string raw, string expectedCode)
    {
        MappingResult<SampleEntity> result = await MapSingleAsync(target, raw);

        result.Succeeded.ShouldBeFalse();
        result.Entity.ShouldBeNull();
        CellConversionError error = result.Errors.ShouldHaveSingleItem();
        error.ErrorCode.ShouldBe(expectedCode);
        error.TargetProperty.ShouldBe(target);
        error.RawValue.ShouldBe(raw);
    }

    // ── Empty cells ──────────────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_empty_required_cell_yields_missing_required()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Text", "  "), ("Count", "5")), Mappings("Text", "Count"),
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        CellConversionError error = result.Errors.ShouldHaveSingleItem();
        error.ErrorCode.ShouldBe(CompiledDataMapper<SampleEntity>.MissingRequiredErrorCode);
        error.TargetProperty.ShouldBe("Text");
    }

    [Fact]
    public async Task MapAsync_empty_optional_cell_leaves_property_default()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Text", "hello"), ("MaybeCount", "")), Mappings("Text", "MaybeCount"),
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Entity!.MaybeCount.ShouldBeNull();
    }

    [Fact]
    public async Task MapAsync_all_mapped_cells_empty_signals_skip()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Text", ""), ("Count", "   "), ("MaybeCount", null)),
            Mappings("Text", "Count", "MaybeCount"),
            Options, TestContext.Current.CancellationToken);

        // Skip signal: no entity AND no errors — even though Text is required.
        result.Entity.ShouldBeNull();
        result.Errors.ShouldBeEmpty();
    }

    // ── Mapping shape errors ─────────────────────────────────────────────────

    [Fact]
    public async Task MapAsync_confirmed_mapping_to_undeclared_property_yields_unknown_property()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Nope", "x")),
            [new ImportColumnMapping("Nope", "Nope", MappingConfidence.Manual)],
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        CellConversionError error = result.Errors.ShouldHaveSingleItem();
        error.ErrorCode.ShouldBe(CompiledDataMapper<SampleEntity>.UnknownPropertyErrorCode);
    }

    [Fact]
    public async Task MapAsync_unmapped_columns_are_ignored()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Text", "hello"), ("Ignored", "junk")),
            [
                new ImportColumnMapping("Text", "Text", MappingConfidence.Manual),
                new ImportColumnMapping("Ignored", null, MappingConfidence.Manual),
            ],
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Entity!.Text.ShouldBe("hello");
    }

    [Fact]
    public async Task MapAsync_missing_cell_for_mapped_column_treated_as_empty()
    {
        CompiledDataMapper<SampleEntity> mapper = CreateMapper();

        MappingResult<SampleEntity> result = await mapper.MapAsync(
            Row(("Text", "hello")), Mappings("Text", "Count"),
            Options, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.Entity!.Count.ShouldBe(0);
    }

    // ── Construction fail-fast ───────────────────────────────────────────────

    [Fact]
    public void Constructor_missing_property_throws_actionable_message()
    {
        SampleImportDefinition definition = new();
        definition.GetBuilder().Properties.Add(new PropertyMapping
        {
            PropertyPath = "DoesNotExist",
            ClrTypeName = "String",
        });

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            new CompiledDataMapper<SampleEntity>(definition));

        ex.Message.ShouldContain("DoesNotExist");
        ex.Message.ShouldContain("IDataMapper<SampleEntity>");
    }

    [Fact]
    public void Constructor_property_without_public_setter_throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            new CompiledDataMapper<NoSetterEntity>(new NoSetterImportDefinition()));

        ex.Message.ShouldContain("no public setter");
        ex.Message.ShouldContain("IDataMapper<NoSetterEntity>");
    }

    [Fact]
    public void Constructor_entity_without_parameterless_ctor_throws()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            new CompiledDataMapper<NoCtorEntity>(new NoCtorImportDefinition()));

        ex.Message.ShouldContain("parameterless constructor");
        ex.Message.ShouldContain("IDataMapper<NoCtorEntity>");
    }

    [Fact]
    public void Constructor_unsupported_property_type_throws_with_unsupported_code()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            new CompiledDataMapper<UnsupportedTypeEntity>(new UnsupportedTypeImportDefinition()));

        ex.Message.ShouldContain(CompiledDataMapper<UnsupportedTypeEntity>.UnsupportedErrorCode);
        ex.Message.ShouldContain("Uri");
    }

    // ── Test fixtures ────────────────────────────────────────────────────────

    internal enum SampleColor
    {
        Red,
        Green,
        Blue,
    }

    internal sealed class SampleEntity
    {
        public string? Text { get; set; }
        public bool Flag { get; set; }
        public Guid Uid { get; set; }
        public SampleColor Color { get; set; }
        public SampleColor? MaybeColor { get; set; }
        public int Count { get; set; }
        public long BigCount { get; set; }
        public short SmallCount { get; set; }
        public byte TinyCount { get; set; }
        public decimal Price { get; set; }
        public double Ratio { get; set; }
        public float Score { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTimeOffset Offset { get; set; }
        public DateOnly Day { get; set; }
        public TimeOnly Time { get; set; }
        public int? MaybeCount { get; set; }
        public DateTime FormattedDate { get; set; }
    }

    internal sealed class SampleImportDefinition : ImportDefinition<SampleEntity>
    {
        public override string Name => "Test.Sample";

        protected override void Configure(ImportDefinitionBuilder<SampleEntity> builder) =>
            builder
                .Property(e => e.Text, p => p.Required())
                .Property(e => e.Flag)
                .Property(e => e.Uid)
                .Property(e => e.Color)
                .Property(e => e.MaybeColor)
                .Property(e => e.Count)
                .Property(e => e.BigCount)
                .Property(e => e.SmallCount)
                .Property(e => e.TinyCount)
                .Property(e => e.Price)
                .Property(e => e.Ratio)
                .Property(e => e.Score)
                .Property(e => e.Timestamp)
                .Property(e => e.Offset)
                .Property(e => e.Day)
                .Property(e => e.Time)
                .Property(e => e.MaybeCount)
                .Property(e => e.FormattedDate, p => p.Format("dd/MM/yyyy"));
    }

    internal sealed class NoSetterEntity
    {
        public string Name { get; } = string.Empty;
    }

    internal sealed class NoSetterImportDefinition : ImportDefinition<NoSetterEntity>
    {
        public override string Name => "Test.NoSetter";

        protected override void Configure(ImportDefinitionBuilder<NoSetterEntity> builder) =>
            builder.Property(e => e.Name);
    }

    internal sealed class NoCtorEntity(string name)
    {
        public string Name { get; set; } = name;
    }

    internal sealed class NoCtorImportDefinition : ImportDefinition<NoCtorEntity>
    {
        public override string Name => "Test.NoCtor";

        protected override void Configure(ImportDefinitionBuilder<NoCtorEntity> builder) =>
            builder.Property(e => e.Name);
    }

    internal sealed class UnsupportedTypeEntity
    {
        public Uri? Link { get; set; }
    }

    internal sealed class UnsupportedTypeImportDefinition : ImportDefinition<UnsupportedTypeEntity>
    {
        public override string Name => "Test.Unsupported";

        protected override void Configure(ImportDefinitionBuilder<UnsupportedTypeEntity> builder) =>
            builder.Property(e => e.Link);
    }
}
