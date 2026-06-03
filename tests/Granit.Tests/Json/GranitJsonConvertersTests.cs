using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Json;
using Shouldly;
using Xunit;

namespace Granit.Tests.Json;

public sealed class GranitJsonConvertersTests
{
    private static int Count<T>(JsonSerializerOptions options) =>
        options.Converters.Count(c => c is T);

    [Fact]
    public void AddGranitJsonConverters_OnEmptyOptions_AddsBothConverters()
    {
        JsonSerializerOptions options = new();

        options.AddGranitJsonConverters();

        Count<JsonStringEnumConverter>(options).ShouldBe(1);
        Count<SingleValueObjectJsonConverterFactory>(options).ShouldBe(1);
    }

    [Fact]
    public void AddGranitJsonConverters_ReturnsSameInstance()
    {
        JsonSerializerOptions options = new();

        options.AddGranitJsonConverters().ShouldBeSameAs(options);
    }

    [Fact]
    public void AddGranitJsonConverters_CalledTwice_DoesNotDuplicate()
    {
        JsonSerializerOptions options = new();

        options.AddGranitJsonConverters();
        options.AddGranitJsonConverters();

        Count<JsonStringEnumConverter>(options).ShouldBe(1);
        Count<SingleValueObjectJsonConverterFactory>(options).ShouldBe(1);
    }

    [Fact]
    public void AddGranitJsonConverters_PreservesPreexistingEnumConverter()
    {
        JsonSerializerOptions options = new();
        var hostConverter = new JsonStringEnumConverter(JsonNamingPolicy.CamelCase);
        options.Converters.Add(hostConverter);

        options.AddGranitJsonConverters();

        // The host's converter is left in place (not duplicated, not replaced)...
        Count<JsonStringEnumConverter>(options).ShouldBe(1);
        options.Converters.OfType<JsonStringEnumConverter>().Single().ShouldBeSameAs(hostConverter);
        // ...while the still-missing factory is added.
        Count<SingleValueObjectJsonConverterFactory>(options).ShouldBe(1);
    }

    [Fact]
    public void AddGranitJsonConverters_PreservesPreexistingFactory()
    {
        JsonSerializerOptions options = new();
        var hostFactory = new SingleValueObjectJsonConverterFactory();
        options.Converters.Add(hostFactory);

        options.AddGranitJsonConverters();

        Count<SingleValueObjectJsonConverterFactory>(options).ShouldBe(1);
        options.Converters.OfType<SingleValueObjectJsonConverterFactory>().Single().ShouldBeSameAs(hostFactory);
        Count<JsonStringEnumConverter>(options).ShouldBe(1);
    }

    [Fact]
    public void AddGranitJsonConverters_NullOptions_Throws()
    {
        JsonSerializerOptions options = null!;

        Should.Throw<ArgumentNullException>(() => options.AddGranitJsonConverters());
    }
}
