using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Internal;
using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Mapping;

public sealed class MappingSuggestionServiceTests
{
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly ISemanticMappingService _semanticService = Substitute.For<ISemanticMappingService>();
    private readonly IOptions<ImportOptions> _options;
    private readonly MappingSuggestionService _sut;

    public MappingSuggestionServiceTests()
    {
        _options = Options.Create(new ImportOptions { FuzzyMatchThreshold = 0.8 });
        _sut = new MappingSuggestionService(_serviceProvider, _semanticService, _options);

        TestPatientImportDefinition definition = new();
        _serviceProvider
            .GetService(typeof(ImportDefinition<TestPatient>))
            .Returns(definition);
    }

    [Fact]
    public async Task Exact_match_on_property_name()
    {
        // Arrange
        List<string> headers = ["Niss", "FirstName", "LastName"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.Count.ShouldBe(3);
        result.ShouldContain(m => m.SourceColumn == "Niss" &&
            m.TargetProperty == "Niss" &&
            m.Confidence == MappingConfidence.Exact);
    }

    [Fact]
    public async Task Exact_match_on_display_name()
    {
        // Arrange
        List<string> headers = ["NISS", "Prénom", "Nom"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldContain(m => m.SourceColumn == "NISS" &&
            m.TargetProperty == "Niss" &&
            m.Confidence == MappingConfidence.Exact);
        result.ShouldContain(m => m.SourceColumn == "Prénom" &&
            m.TargetProperty == "FirstName" &&
            m.Confidence == MappingConfidence.Exact);
        result.ShouldContain(m => m.SourceColumn == "Nom" &&
            m.TargetProperty == "LastName" &&
            m.Confidence == MappingConfidence.Exact);
    }

    [Fact]
    public async Task Exact_match_on_alias()
    {
        // Arrange
        List<string> headers = ["Numéro national", "First Name", "Courriel"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldContain(m => m.SourceColumn == "Numéro national" &&
            m.TargetProperty == "Niss" &&
            m.Confidence == MappingConfidence.Exact);
        result.ShouldContain(m => m.SourceColumn == "Courriel" &&
            m.TargetProperty == "Email" &&
            m.Confidence == MappingConfidence.Exact);
    }

    [Fact]
    public async Task Fuzzy_match_above_threshold()
    {
        // Arrange — "Prenom" is close to "Prénom" (display name)
        List<string> headers = ["Prenom"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldContain(m => m.SourceColumn == "Prenom" &&
            m.TargetProperty == "FirstName" &&
            m.Confidence == MappingConfidence.Fuzzy);
    }

    [Fact]
    public async Task No_match_below_threshold()
    {
        // Arrange — "XYZ123" is far from any property
        List<string> headers = ["XYZ123"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotContain(m => m.SourceColumn == "XYZ123");
    }

    [Fact]
    public async Task Saved_mappings_have_priority()
    {
        // Arrange
        IMappingReader store = Substitute.For<IMappingReader>();
        store.LoadAsync("Test.PatientImport", Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ImportColumnMapping>>(
            [
                new("My Custom Column", "Niss", MappingConfidence.Saved),
            ]);
        _serviceProvider.GetService(typeof(IMappingReader)).Returns(store);

        List<string> headers = ["My Custom Column", "Niss"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldContain(m => m.SourceColumn == "My Custom Column" &&
            m.TargetProperty == "Niss" &&
            m.Confidence == MappingConfidence.Saved);
        // "Niss" header should not also match to Niss (already taken)
        result.ShouldNotContain(m => m.SourceColumn == "Niss" &&
            m.TargetProperty == "Niss");
    }

    [Fact]
    public async Task Semantic_tier_called_for_unmapped_headers()
    {
        // Arrange
        _semanticService.IsAvailable.Returns(true);
        _semanticService.SuggestSemanticMappingsAsync(
                Arg.Any<IReadOnlyList<string>>(),
                Arg.Any<IReadOnlyList<ImportFieldMetadata>>(),
                Arg.Any<IReadOnlyList<string[]>?>(),
                Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<SemanticMappingSuggestion>>(
            [
                new("Adresse courriel", "Email", 0.95),
            ]);

        List<string> headers = ["Niss", "Adresse courriel"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldContain(m => m.SourceColumn == "Adresse courriel" &&
            m.TargetProperty == "Email" &&
            m.Confidence == MappingConfidence.Semantic);
    }

    [Fact]
    public async Task Semantic_tier_skipped_when_unavailable()
    {
        // Arrange
        _semanticService.IsAvailable.Returns(false);
        List<string> headers = ["Unknown Column"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert
        await _semanticService.DidNotReceive().SuggestSemanticMappingsAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<IReadOnlyList<ImportFieldMetadata>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Deduplication_keeps_best_confidence()
    {
        // Arrange — "Email" matches exactly; ensure it's not matched twice
        List<string> headers = ["Email", "Mail"];

        // Act
        IReadOnlyList<ImportColumnMapping> result = await _sut.SuggestMappingsAsync<TestPatient>(
            headers, TestContext.Current.CancellationToken);

        // Assert — only one mapping to "Email" target
        result.Count(m => m.TargetProperty == "Email").ShouldBe(1);
    }
}
