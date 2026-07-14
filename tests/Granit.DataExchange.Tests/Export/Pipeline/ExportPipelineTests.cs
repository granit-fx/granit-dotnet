using System.Runtime.CompilerServices;
using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Internal;
using Granit.DataExchange.Export.Pipeline;
using Granit.QueryEngine;
using Granit.QueryEngine.Meta;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Export.Pipeline;

public sealed class ExportPipelineTests
{
    // ── Compiled property-path getters ───────────────────────────────────

    [Fact]
    public async Task WriteAsync_scalar_and_dotted_paths_produce_aligned_rows()
    {
        // Arrange
        ExportPipeline<Person> pipeline = CreatePipeline(new PersonExportDefinition());
        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor("Name", "String", "Nom", null, 0, false),
            new ExportFieldDescriptor("Company.Name", "String", null, null, 1, true));

        // Act
        await using MemoryStream output = new();
        await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert — values aligned to the fields index
        writer.Rows.Count.ShouldBe(2);
        writer.Rows[0][0].ShouldBe("Alice");
        writer.Rows[0][1].ShouldBe("Acme Corp");
        writer.Rows[1][0].ShouldBe("Jane");
    }

    [Fact]
    public async Task WriteAsync_dotted_path_with_null_intermediate_yields_null_cell()
    {
        // Arrange — Jane has Company = null: the compiled getter must null-propagate
        ExportPipeline<Person> pipeline = CreatePipeline(new PersonExportDefinition());
        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor("Company.Name", "String", null, null, 0, true));

        // Act — must not throw NullReferenceException
        await using MemoryStream output = new();
        await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert
        writer.Rows.Count.ShouldBe(2);
        writer.Rows[0][0].ShouldBe("Acme Corp");
        writer.Rows[1][0].ShouldBeNull();
    }

    [Fact]
    public async Task WriteAsync_unknown_property_path_yields_null_cell()
    {
        // Arrange — a path that does not exist on the entity resolves to null (legacy behavior)
        ExportPipeline<Person> pipeline = CreatePipeline(new PersonExportDefinition());
        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor("DoesNotExist", "String", null, null, 0, false));

        // Act
        await using MemoryStream output = new();
        await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert
        writer.Rows.ShouldAllBe(r => r[0] == null);
    }

    // ── ValueSelector fields ─────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_value_selector_field_uses_selector()
    {
        // Arrange
        ExportPipeline<Person> pipeline = CreatePipeline(new PersonExportDefinition());
        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor(
                "Tags", "List`1", null, null, 0, false,
                RequiresHierarchy: true,
                ValueSelector: entity => ((Person)entity).Tags,
                SelectorType: typeof(List<string>)));

        // Act
        await using MemoryStream output = new();
        await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert
        writer.Rows.Count.ShouldBe(2);
        writer.Rows[0][0].ShouldBeAssignableTo<List<string>>();
        ((List<string>)writer.Rows[0][0]!).ShouldBe(["dotnet", "export"]);
    }

    // ── Extra-metadata fields ────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_extra_metadata_field_routes_through_extra_value_resolver()
    {
        // Arrange — definition opts into metadata; "Extra1" is declared by the field provider
        ExportFieldDescriptor extraField = new("Extra1", "String", null, null, 99, false);
        IExtraExportFieldProvider extraFieldProvider = Substitute.For<IExtraExportFieldProvider>();
        extraFieldProvider.GetExtraFields(typeof(Person)).Returns([extraField]);

        IExportExtraValueResolver extraValueResolver = Substitute.For<IExportExtraValueResolver>();
        extraValueResolver.ResolveExtraValue(Arg.Any<object>(), "Extra1").Returns("extra-value");

        ExportPipeline<Person> pipeline = CreatePipeline(
            new MetadataPersonExportDefinition(),
            extraFieldProvider: extraFieldProvider,
            extraValueResolver: extraValueResolver);

        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor("Name", "String", null, null, 0, false),
            extraField);

        // Act
        await using MemoryStream output = new();
        await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert — extra field resolved via IExportExtraValueResolver, not property reflection
        writer.Rows.Count.ShouldBe(2);
        writer.Rows[0][1].ShouldBe("extra-value");
        extraValueResolver.Received(2).ResolveExtraValue(Arg.Any<object>(), "Extra1");
    }

    // ── Query-engine path ────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_with_query_definition_streams_through_query_engine()
    {
        // Arrange
        FakeQueryEngine queryEngine = new();
        ExportPipeline<Person> pipeline = CreatePipeline(
            new QueryPersonExportDefinition(),
            queryEngine: queryEngine);

        CollectingExportWriter writer = new();
        ExportPipelineContext context = new()
        {
            Request = new ExportRequest(
                "Test.QueryPeople", "csv", null, false,
                Sort: "-Name",
                Filter: new Dictionary<string, string> { ["name.eq"] = "Alice" },
                Presets: null,
                Search: "hello"),
            Fields = [new ExportFieldDescriptor("Name", "String", null, null, 0, false)],
            Writer = writer,
        };

        // Act
        await using MemoryStream output = new();
        long rowCount = await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert — typed IQueryEngine<TEntity> invoked with the request's query parameters
        queryEngine.StreamCalled.ShouldBeTrue();
        queryEngine.CapturedRequest.ShouldNotBeNull();
        queryEngine.CapturedRequest!.Sort.ShouldBe("-Name");
        queryEngine.CapturedRequest.Filter!["name.eq"].ShouldBe("Alice");
        queryEngine.CapturedRequest.Search.ShouldBe("hello");
        rowCount.ShouldBe(2);
    }

    // ── Row count ────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteAsync_returns_writer_row_count()
    {
        // Arrange
        ExportPipeline<Person> pipeline = CreatePipeline(new PersonExportDefinition());
        CollectingExportWriter writer = new();
        ExportPipelineContext context = BuildContext(
            writer,
            new ExportFieldDescriptor("Name", "String", null, null, 0, false));

        // Act
        await using MemoryStream output = new();
        long rowCount = await pipeline.WriteAsync(context, output, TestContext.Current.CancellationToken);

        // Assert
        rowCount.ShouldBe(2);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static ExportPipeline<Person> CreatePipeline(
        ExportDefinition<Person> definition,
        IQueryEngine<Person>? queryEngine = null,
        IExtraExportFieldProvider? extraFieldProvider = null,
        IExportExtraValueResolver? extraValueResolver = null) =>
        new(
            definition,
            new PersonDataSource(),
            queryEngine,
            extraFieldProvider ?? new NullExtraExportFieldProvider(),
            extraValueResolver ?? new NullExportExtraValueResolver());

    private static ExportPipelineContext BuildContext(
        IExportWriter writer,
        params ExportFieldDescriptor[] fields) =>
        new()
        {
            Request = new ExportRequest("Test.People", "csv", null, false, null, null, null, null),
            Fields = fields,
            Writer = writer,
        };

    private sealed class CollectingExportWriter : IExportWriter
    {
        public List<object?[]> Rows { get; } = [];

        public bool CanWrite(string format) => true;

        public string MimeType => "text/csv";

        public string FileExtension => ".csv";

        public async Task<long> WriteAsync(
            Stream output,
            IReadOnlyList<ExportFieldDescriptor> fields,
            IAsyncEnumerable<object?[]> rows,
            CancellationToken cancellationToken = default)
        {
            await foreach (object?[] row in rows.WithCancellation(cancellationToken))
            {
                Rows.Add(row);
            }

            return Rows.Count;
        }
    }

    // ── Test types ────────────────────────────────────────────────────────

    private sealed class Person
    {
        public string Name { get; set; } = string.Empty;
        public Company? Company { get; set; }
        public List<string> Tags { get; set; } = [];
    }

    private sealed class Company
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class PersonExportDefinition : ExportDefinition<Person>
    {
        public override string Name => "Test.People";

        protected override void Configure(ExportDefinitionBuilder<Person> builder) =>
            builder
                .Field(p => p.Name)
                .Field(p => p.Company, c => c.Name);
    }

    private sealed class MetadataPersonExportDefinition : ExportDefinition<Person>
    {
        public override string Name => "Test.MetaPeople";

        protected override void Configure(ExportDefinitionBuilder<Person> builder) =>
            builder
                .Field(p => p.Name)
                .IncludeMetadata();
    }

    private sealed class QueryPersonExportDefinition : ExportDefinition<Person>
    {
        public override string Name => "Test.QueryPeople";
        public override string? QueryDefinitionName => "Test.PeopleQuery";

        protected override void Configure(ExportDefinitionBuilder<Person> builder) =>
            builder.Field(p => p.Name);
    }

    private sealed class PersonDataSource : IExportDataSource<Person>
    {
        public IQueryable<Person> GetQueryable() =>
            new List<Person>
            {
                new()
                {
                    Name = "Alice",
                    Company = new Company { Name = "Acme Corp" },
                    Tags = ["dotnet", "export"],
                },
                new()
                {
                    Name = "Jane",
                    Company = null,
                    Tags = [],
                },
            }.AsQueryable();
    }

    private sealed class FakeQueryEngine : IQueryEngine<Person>
    {
        public bool StreamCalled { get; private set; }
        public QueryRequest? CapturedRequest { get; private set; }

        public async IAsyncEnumerable<Person> ExecuteStreamAsync(
            IQueryable<Person> source,
            QueryRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            StreamCalled = true;
            CapturedRequest = request;
            await Task.CompletedTask.ConfigureAwait(false);
            foreach (Person item in source)
            {
                yield return item;
            }
        }

        public Task<PagedResult<Person>> ExecuteAsync(
            IQueryable<Person> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<PagedResult<TProjection>> ExecuteAsync<TProjection>(
            IQueryable<Person> source, QueryRequest request,
            System.Linq.Expressions.Expression<Func<Person, TProjection>> projection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroupedResult<Person>> ExecuteGroupedAsync(
            IQueryable<Person> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GroupedResult<TProjection>> ExecuteGroupedAsync<TProjection>(
            IQueryable<Person> source, QueryRequest request,
            System.Linq.Expressions.Expression<Func<Person, TProjection>> projection,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public QueryMetadata GetMetadata() =>
            throw new NotSupportedException();

        public IQueryable<Person> BuildFilteredQuery(IQueryable<Person> source, QueryRequest request) =>
            throw new NotSupportedException();
    }
}
