using Granit.Guids.Extensions;
using Granit.Guids.Options;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Guids.Tests;

public sealed class GuidsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitGuids_DefaultStrategy_RegistersUuidV7Generator()
    {
        ServiceCollection services = new();
        services.AddOptions();

        services.AddGranitGuids();

        using ServiceProvider sp = services.BuildServiceProvider();
        IGuidGenerator generator = sp.GetRequiredService<IGuidGenerator>();
        generator.ShouldBeOfType<UuidV7GuidGenerator>();
    }

    [Fact]
    public void AddGranitGuids_SequentialStrategy_RegistersSequentialGenerator()
    {
        ServiceCollection services = new();
        services.AddOptions();

        services.AddGranitGuids(opts => opts.Strategy = GuidStrategy.Sequential);

        using ServiceProvider sp = services.BuildServiceProvider();
        IGuidGenerator generator = sp.GetRequiredService<IGuidGenerator>();
        generator.ShouldBeOfType<SequentialGuidGenerator>();
    }

    [Fact]
    public void AddGranitGuids_RandomStrategy_RegistersSimpleGenerator()
    {
        ServiceCollection services = new();
        services.AddOptions();

        services.AddGranitGuids(opts => opts.Strategy = GuidStrategy.Random);

        using ServiceProvider sp = services.BuildServiceProvider();
        IGuidGenerator generator = sp.GetRequiredService<IGuidGenerator>();
        generator.ShouldBeOfType<SimpleGuidGenerator>();
    }

    [Fact]
    public void AddGranitGuids_TryAddSingleton_DoesNotOverrideExisting()
    {
        ServiceCollection services = new();
        IGuidGenerator customGenerator = NSubstitute.Substitute.For<IGuidGenerator>();
        services.AddSingleton(customGenerator);

        services.AddGranitGuids();

        using ServiceProvider sp = services.BuildServiceProvider();
        IGuidGenerator resolved = sp.GetRequiredService<IGuidGenerator>();
        resolved.ShouldBeSameAs(customGenerator);
    }

    [Fact]
    public void AddGranitGuids_WithConfigure_AppliesOptions()
    {
        ServiceCollection services = new();

        services.AddGranitGuids(opts =>
        {
            opts.Strategy = GuidStrategy.Sequential;
            opts.DefaultSequentialGuidType = SequentialGuidType.SequentialAsBinary;
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        GuidGeneratorOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<GuidGeneratorOptions>>().Value;
        options.Strategy.ShouldBe(GuidStrategy.Sequential);
        options.DefaultSequentialGuidType.ShouldBe(SequentialGuidType.SequentialAsBinary);
    }
}
