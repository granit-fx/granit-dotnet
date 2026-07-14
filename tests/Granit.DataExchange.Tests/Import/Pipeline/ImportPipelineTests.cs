using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Execution;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Granit.DataExchange.Import.Parsing;
using Granit.DataExchange.Import.Pipeline;
using Granit.DataExchange.Import.Reporting;
using Granit.DataExchange.Import.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Pipeline;

/// <summary>
/// Errors-as-data guarantees of <see cref="ImportPipeline{TEntity}"/>: mapper and validator
/// failures reach the executor as Failed outcomes, empty rows as Skipped — nothing is dropped.
/// </summary>
public sealed class ImportPipelineTests
{
    private static readonly IReadOnlyList<ImportColumnMapping> DefaultMappings =
    [
        new ImportColumnMapping("Name", "Name", MappingConfidence.Exact),
        new ImportColumnMapping("Age", "Age", MappingConfidence.Exact),
    ];

    private static ImportPipelineContext CreateContext(Stream? stream = null) => new()
    {
        FileStream = stream ?? new MemoryStream([1]),
        MimeType = "text/fake",
        Mappings = DefaultMappings,
        ExecutionOptions = new ImportExecutionOptions(),
    };

    private static IImportPipeline CreatePipeline(Action<ServiceCollection> configure)
    {
        ServiceCollection services = new();
        services.AddSingleton<IOptions<ImportOptions>>(Options.Create(new ImportOptions()));
        configure(services);
        ServiceProvider provider = services.BuildServiceProvider();

        PatientDefinition definition = new();
        return new ImportPipelineDescriptor<Patient>(definition).Create(provider);
    }

    [Fact]
    public async Task ExecuteAsync_mapper_and_validator_failures_reach_executor_and_report()
    {
        // Arrange — 5 rows: 2 good, 1 conversion-broken, 1 validation-broken, 1 all-empty
        FakeParser parser = new(
        [
            NewRow(1, "Alice", "30"),
            NewRow(2, "Bob", "notanumber"),   // conversion failure
            NewRow(3, "Reject", "40"),        // validator rejects entities named "Reject"
            NewRow(4, "", ""),                // all-empty → skipped
            NewRow(5, "Carol", "50"),
        ]);
        CollectingExecutor executor = new();

        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(parser);
            services.AddSingleton<IImportExecutor<Patient>>(executor);
            services.AddSingleton<IRowValidator<Patient>>(new RejectingValidator());
        });

        // Act
        ImportReport report = await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert — every source row surfaces as exactly one outcome
        executor.Outcomes.Count.ShouldBe(5);

        RowOutcome<Patient> ok1 = executor.Outcomes[0];
        ok1.Entity.ShouldNotBeNull();
        ok1.Entity!.Name.ShouldBe("Alice");
        ok1.Entity.Age.ShouldBe(30);

        RowOutcome<Patient> conversionFailed = executor.Outcomes[1];
        conversionFailed.Error.ShouldNotBeNull();
        conversionFailed.Error!.Kind.ShouldBe(ImportRowErrorKind.Conversion);
        conversionFailed.Error.ErrorCodes.ShouldContain("Granit:DataExchange:Conversion:InvalidFormat");
        conversionFailed.Error.RowNumber.ShouldBe(2);

        RowOutcome<Patient> validationFailed = executor.Outcomes[2];
        validationFailed.Error.ShouldNotBeNull();
        validationFailed.Error!.Kind.ShouldBe(ImportRowErrorKind.Validation);
        validationFailed.Error.ErrorCodes.ShouldContain("Test:Validation:NameRejected");

        executor.Outcomes[3].IsSkipped.ShouldBeTrue();
        executor.Outcomes[4].Entity!.Name.ShouldBe("Carol");

        // The report reflects the outcomes (built by the fake executor from what it received)
        report.TotalRows.ShouldBe(5);
        report.SucceededRows.ShouldBe(2);
        report.FailedRows.ShouldBe(2);
        report.SkippedRows.ShouldBe(1);
        report.RowErrors.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_identity_resolver_exception_becomes_failed_outcome()
    {
        FakeParser parser = new([NewRow(1, "Alice", "30")]);
        CollectingExecutor executor = new();

        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(parser);
            services.AddSingleton<IImportExecutor<Patient>>(executor);
            services.AddSingleton<Granit.DataExchange.Import.Identity.IRecordIdentityResolver<Patient>>(
                new ThrowingIdentityResolver());
        });

        await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        RowOutcome<Patient> outcome = executor.Outcomes.ShouldHaveSingleItem();
        outcome.Error.ShouldNotBeNull();
        outcome.Error!.Kind.ShouldBe(ImportRowErrorKind.Identity);
        outcome.Error.ErrorCodes.ShouldContain("Granit:DataExchange:Identity:ResolutionFailed");
        outcome.Error.Message.ShouldContain("ambiguous match");
    }

    [Fact]
    public async Task ExecuteAsync_identity_resolver_is_called_in_chunks_of_batch_size()
    {
        FakeParser parser = new(
        [
            NewRow(1, "Alice", "30"),
            NewRow(2, "Bob", "31"),
            NewRow(3, "Carol", "32"),
        ]);
        CollectingExecutor executor = new();
        StubIdentityResolver resolver = new();

        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(parser);
            services.AddSingleton<IImportExecutor<Patient>>(executor);
            services.AddSingleton<Granit.DataExchange.Import.Identity.IRecordIdentityResolver<Patient>>(resolver);
        });

        ImportPipelineContext context = CreateContext() with
        {
            ExecutionOptions = new ImportExecutionOptions { BatchSize = 2 },
        };

        await pipeline.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // 3 rows, batch size 2 → one chunk of 2 and one chunk of 1 (never one call per row).
        resolver.BatchSizesSeen.ShouldBe([2, 1]);

        executor.Outcomes.Count.ShouldBe(3);
        executor.Outcomes.Select(o => o.Entity!.Name).ShouldBe(["Alice", "Bob", "Carol"]);
        executor.Outcomes.ShouldAllBe(o => o.Identity!.Operation == Granit.DataExchange.Import.Identity.RecordOperation.Upsert);
    }

    [Fact]
    public async Task ExecuteAsync_no_resolver_and_no_business_key_defaults_to_insert()
    {
        FakeParser parser = new([NewRow(1, "Alice", "30")]);
        CollectingExecutor executor = new();

        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(parser);
            services.AddSingleton<IImportExecutor<Patient>>(executor);
        });

        await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        RowOutcome<Patient> outcome = executor.Outcomes.ShouldHaveSingleItem();
        outcome.Identity!.Operation.ShouldBe(Granit.DataExchange.Import.Identity.RecordOperation.Insert);
    }

    [Fact]
    public async Task ExecuteAsync_no_resolver_and_declared_business_key_defaults_to_upsert()
    {
        FakeParser parser = new([NewRow(1, "Alice", "30")]);
        CollectingExecutor executor = new();

        ServiceCollection services = new();
        services.AddSingleton<IOptions<ImportOptions>>(Options.Create(new ImportOptions()));
        services.AddSingleton<IFileParser>(parser);
        services.AddSingleton<IImportExecutor<Patient>>(executor);
        ServiceProvider provider = services.BuildServiceProvider();

        PatientWithBusinessKeyDefinition definition = new();
        IImportPipeline pipeline = new ImportPipelineDescriptor<Patient>(definition).Create(provider);

        await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        RowOutcome<Patient> outcome = executor.Outcomes.ShouldHaveSingleItem();
        outcome.Identity!.Operation.ShouldBe(Granit.DataExchange.Import.Identity.RecordOperation.Upsert);
        outcome.Identity.Key.ShouldBe(new Granit.DataExchange.Import.Identity.EntityKey("Alice"));
    }

    [Fact]
    public async Task ExecuteAsync_no_resolver_missing_business_key_component_is_ambiguous()
    {
        FakeParser parser = new([NewRow(1, "", "30")]);
        CollectingExecutor executor = new();

        ServiceCollection services = new();
        services.AddSingleton<IOptions<ImportOptions>>(Options.Create(new ImportOptions()));
        services.AddSingleton<IFileParser>(parser);
        services.AddSingleton<IImportExecutor<Patient>>(executor);
        ServiceProvider provider = services.BuildServiceProvider();

        PatientWithBusinessKeyDefinition definition = new();
        IImportPipeline pipeline = new ImportPipelineDescriptor<Patient>(definition).Create(provider);

        await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        RowOutcome<Patient> outcome = executor.Outcomes.ShouldHaveSingleItem();
        outcome.Identity!.Operation.ShouldBe(Granit.DataExchange.Import.Identity.RecordOperation.Ambiguous);
        outcome.Identity.ReasonCodes.ShouldContain(Granit.DataExchange.Import.Identity.IdentityReasonCodes.MissingKeyComponent);
    }

    [Fact]
    public async Task ExecuteAsync_no_parser_for_mime_type_lists_registered_parsers()
    {
        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(new FakeParser([])); // CanParse only "text/fake"
            services.AddSingleton<IImportExecutor<Patient>>(new CollectingExecutor());
        });

        ImportPipelineContext context = CreateContext() with { MimeType = "application/x-unknown" };

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(context, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("No IFileParser registered for MIME type 'application/x-unknown'");
        ex.Message.ShouldContain(nameof(FakeParser));
    }

    [Fact]
    public async Task ExecuteAsync_no_parser_registered_at_all_says_none()
    {
        IImportPipeline pipeline = CreatePipeline(services =>
            services.AddSingleton<IImportExecutor<Patient>>(new CollectingExecutor()));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("[none]");
    }

    [Fact]
    public async Task ExecuteAsync_missing_executor_fails_fast_pointing_at_registration()
    {
        IImportPipeline pipeline = CreatePipeline(services =>
            services.AddSingleton<IFileParser>(new FakeParser([])));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("No IImportExecutor<Patient>");
        ex.Message.ShouldContain("AddImportExecutor<Patient, TContext>()");
    }

    [Fact]
    public async Task ExecuteAsync_di_registered_mapper_overrides_compiled_default()
    {
        FakeParser parser = new([NewRow(1, "Alice", "30")]);
        CollectingExecutor executor = new();

        IImportPipeline pipeline = CreatePipeline(services =>
        {
            services.AddSingleton<IFileParser>(parser);
            services.AddSingleton<IImportExecutor<Patient>>(executor);
            services.AddSingleton<IDataMapper<Patient>>(new ConstantMapper());
        });

        await pipeline.ExecuteAsync(CreateContext(), TestContext.Current.CancellationToken);

        executor.Outcomes.ShouldHaveSingleItem().Entity!.Name.ShouldBe("FromCustomMapper");
    }

    // ── Test fixtures ────────────────────────────────────────────────────────

    private static RawImportRow NewRow(int number, string name, string age) =>
        new(number, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Name"] = name,
            ["Age"] = age,
        });

    internal sealed class Patient
    {
        public string? Name { get; set; }
        public int Age { get; set; }
    }

    internal sealed class PatientDefinition : ImportDefinition<Patient>
    {
        public override string Name => "Test.Patients";

        protected override void Configure(ImportDefinitionBuilder<Patient> builder) =>
            builder
                .Property(e => e.Name, p => p.Required())
                .Property(e => e.Age);
    }

    internal sealed class PatientWithBusinessKeyDefinition : ImportDefinition<Patient>
    {
        public override string Name => "Test.PatientsWithBusinessKey";

        protected override void Configure(ImportDefinitionBuilder<Patient> builder) =>
            builder
                .HasBusinessKey(e => e.Name)
                .Property(e => e.Name)
                .Property(e => e.Age);
    }

    private sealed class FakeParser(IReadOnlyList<RawImportRow> rows) : IFileParser
    {
        public bool CanParse(string mimeType) => mimeType == "text/fake";

        public Task<IReadOnlyList<string>> ExtractHeadersAsync(
            Stream stream, FileParsingOptions options, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>(["Name", "Age"]);

        public Task<IReadOnlyList<string[]>> ReadPreviewAsync(
            Stream stream, FileParsingOptions options, int maxRows = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string[]>>([]);

        public async IAsyncEnumerable<RawImportRow> ParseAsync(
            Stream stream, FileParsingOptions options,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (RawImportRow row in rows)
            {
                yield return row;
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Captures every outcome it receives and folds them into a report —
    /// proves the pipeline hands over failures instead of dropping them.
    /// </summary>
    private sealed class CollectingExecutor : IImportExecutor<Patient>
    {
        public List<RowOutcome<Patient>> Outcomes { get; } = [];

        public async Task<ImportReport> ExecuteAsync(
            IAsyncEnumerable<RowOutcome<Patient>> rows,
            ImportExecutionOptions options,
            IProgress<ImportProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            await foreach (RowOutcome<Patient> row in rows.WithCancellation(cancellationToken))
            {
                Outcomes.Add(row);
            }

            return new ImportReport
            {
                TotalRows = Outcomes.Count,
                SucceededRows = Outcomes.Count(o => o.Entity is not null),
                FailedRows = Outcomes.Count(o => o.Error is not null),
                SkippedRows = Outcomes.Count(o => o.IsSkipped),
                InsertedRows = Outcomes.Count(o => o.Entity is not null),
                UpdatedRows = 0,
                Duration = TimeSpan.Zero,
                FinalStatus = Granit.DataExchange.Import.Domain.ImportJobStatus.Completed,
                RowErrors = [.. Outcomes.Where(o => o.Error is not null).Select(o => o.Error!)],
            };
        }
    }

    private sealed class RejectingValidator : IRowValidator<Patient>
    {
        public Task<RowValidationResult> ValidateAsync(
            Patient entity, int rowNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(entity.Name == "Reject"
                ? new RowValidationResult
                {
                    Errors = [new RowFieldError("Name", "Test:Validation:NameRejected", "Name is rejected.")],
                }
                : new RowValidationResult());
    }

    private sealed class ThrowingIdentityResolver : Granit.DataExchange.Import.Identity.IRecordIdentityResolver<Patient>
    {
        public Task<IReadOnlyList<Granit.DataExchange.Import.Identity.RecordIdentity>> ResolveBatchAsync(
            IReadOnlyList<Patient> batch, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("ambiguous match");
    }

    /// <summary>Stamps every row as Upsert on a fixed key so the pipeline's batching can be observed.</summary>
    private sealed class StubIdentityResolver : Granit.DataExchange.Import.Identity.IRecordIdentityResolver<Patient>
    {
        public List<int> BatchSizesSeen { get; } = [];

        public Task<IReadOnlyList<Granit.DataExchange.Import.Identity.RecordIdentity>> ResolveBatchAsync(
            IReadOnlyList<Patient> batch, CancellationToken cancellationToken = default)
        {
            BatchSizesSeen.Add(batch.Count);
            IReadOnlyList<Granit.DataExchange.Import.Identity.RecordIdentity> identities =
            [
                .. batch.Select(p => Granit.DataExchange.Import.Identity.RecordIdentity.Upsert(
                    new Granit.DataExchange.Import.Identity.EntityKey(p.Name),
                    Granit.DataExchange.Import.Identity.EntityKeyKind.BusinessKey)),
            ];
            return Task.FromResult(identities);
        }
    }

    private sealed class ConstantMapper : IDataMapper<Patient>
    {
        public Task<MappingResult<Patient>> MapAsync(
            RawImportRow row,
            IReadOnlyList<ImportColumnMapping> mappings,
            ImportOptions options,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new MappingResult<Patient> { Entity = new Patient { Name = "FromCustomMapper" } });
    }
}
