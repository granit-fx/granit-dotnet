// =============================================================================
// SettingDefinitionRegistryTests - Tests unitaires du registre de définitions
// =============================================================================

using Granit.Settings.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingDefinitionRegistryTests
{
    private sealed class FakeProvider(params SettingDefinition[] definitions) : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            foreach (SettingDefinition def in definitions)
            {
                context.Add(def);
            }
        }
    }

    [Fact]
    public void NoProviders_Returns_EmptyCollection()
    {
        SettingDefinitionRegistry manager = new([]);

        manager.GetAll().ShouldBeEmpty();
    }

    [Fact]
    public void Get_KnownSetting_Returns_Definition()
    {
        SettingDefinition def = new("App.Theme") { DefaultValue = "dark" };
        SettingDefinitionRegistry manager = new([new FakeProvider(def)]);

        SettingDefinition result = manager.Get("App.Theme");

        result.ShouldBeSameAs(def);
    }

    [Fact]
    public void Get_UnknownSetting_Throws_InvalidOperationException()
    {
        SettingDefinitionRegistry manager = new([]);

        Action act = () => manager.Get("Unknown.Setting");

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("Unknown.Setting");
    }

    [Fact]
    public void GetOrNull_UnknownSetting_Returns_Null()
    {
        SettingDefinitionRegistry manager = new([]);

        SettingDefinition? result = manager.GetOrNull("Unknown.Setting");

        result.ShouldBeNull();
    }

    [Fact]
    public void GetAll_Returns_AllDeclaredDefinitions()
    {
        SettingDefinition def1 = new("App.Theme");
        SettingDefinition def2 = new("App.Language");
        SettingDefinitionRegistry manager = new([new FakeProvider(def1, def2)]);

        IReadOnlyCollection<SettingDefinition> all = manager.GetAll();

        all.Count.ShouldBe(2);
        all.ShouldContain(def1);
        all.ShouldContain(def2);
    }

    [Fact]
    public void LaterProvider_Overwrites_SameNameDefinition()
    {
        SettingDefinition first = new("App.Theme") { DefaultValue = "dark" };
        SettingDefinition second = new("App.Theme") { DefaultValue = "light" };
        FakeProvider providerA = new(first);
        FakeProvider providerB = new(second);

        SettingDefinitionRegistry manager = new([providerA, providerB]);

        manager.Get("App.Theme").DefaultValue.ShouldBe("light", "le second provider écrase le premier");
    }

    [Fact]
    public void DefinitionContext_GetOrNull_Returns_AlreadyAddedDefinition()
    {
        SettingDefinition added = new("App.Theme");
        SettingDefinition? capturedFromContext = null;

        var provider = new FakeProvider(added);

        // On utilise un provider qui consulte le contexte pendant Define()
        SettingDefinitionRegistry manager = new([
            new InspectingProvider(added, ctx =>
            {
                capturedFromContext = ctx.GetOrNull("App.Theme");
            })
        ]);

        capturedFromContext.ShouldBeSameAs(added,
            "GetOrNull doit retrouver la définition ajoutée dans le même contexte");
    }

    private sealed class InspectingProvider(
        SettingDefinition definition,
        Action<ISettingDefinitionContext> inspect) : ISettingDefinitionProvider
    {
        public void Define(ISettingDefinitionContext context)
        {
            context.Add(definition);
            inspect(context);
        }
    }

    // -------------------------------------------------------------------------
    // Registration-time invariant validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_InvalidDefaultValue_ForValueKind_Throws()
    {
        SettingDefinition def = new("App.MaxRetries")
        {
            ValueKind = ValueKind.Int,
            DefaultValue = "not-a-number",
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => new SettingDefinitionRegistry([new FakeProvider(def)]));
        ex.Message.ShouldContain("App.MaxRetries");
    }

    [Fact]
    public void Constructor_DefaultValueOutsideAllowedValues_Throws()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            AllowedValues = ["Debug", "Information"],
            DefaultValue = "Trace",
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(
            () => new SettingDefinitionRegistry([new FakeProvider(def)]));
        ex.Message.ShouldContain("App.LogLevel");
        ex.Message.ShouldContain("AllowedValues");
    }

    [Fact]
    public void Constructor_ValidDefinition_DoesNotThrow()
    {
        SettingDefinition def = new("App.MaxRetries")
        {
            ValueKind = ValueKind.Int,
            AllowedValues = ["1", "5", "10"],
            DefaultValue = "5",
        };

        Should.NotThrow(() => new SettingDefinitionRegistry([new FakeProvider(def)]));
    }
}
