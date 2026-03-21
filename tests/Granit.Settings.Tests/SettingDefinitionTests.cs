using Granit.Settings.Definitions;
using Shouldly;
using Xunit;

namespace Granit.Settings.Tests;

public sealed class SettingDefinitionTests
{
    // -------------------------------------------------------------------------
    // Constructor validation
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullName_Throws_ArgumentException()
    {
        Action act = () => _ = new SettingDefinition(null!);

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_EmptyName_Throws_ArgumentException()
    {
        Action act = () => _ = new SettingDefinition("");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_WhitespaceName_Throws_ArgumentException()
    {
        Action act = () => _ = new SettingDefinition("   ");

        Should.Throw<ArgumentException>(act);
    }

    [Fact]
    public void Constructor_ValidName_SetsNameProperty()
    {
        SettingDefinition def = new("App.Theme");

        def.Name.ShouldBe("App.Theme");
    }

    // -------------------------------------------------------------------------
    // Default property values
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultValue_IsNull_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.DefaultValue.ShouldBeNull();
    }

    [Fact]
    public void IsEncrypted_IsFalse_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.IsEncrypted.ShouldBeFalse();
    }

    [Fact]
    public void IsVisibleToClients_IsFalse_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.IsVisibleToClients.ShouldBeFalse();
    }

    [Fact]
    public void IsInherited_IsTrue_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.IsInherited.ShouldBeTrue();
    }

    [Fact]
    public void Providers_IsEmpty_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.Providers.ShouldBeEmpty();
    }

    [Fact]
    public void DisplayName_IsNull_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.DisplayName.ShouldBeNull();
    }

    [Fact]
    public void Description_IsNull_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.Description.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Init properties
    // -------------------------------------------------------------------------

    [Fact]
    public void AllProperties_CanBeSet_ViaInitSyntax()
    {
        SettingDefinition def = new("App.Secret")
        {
            DefaultValue = "default",
            IsEncrypted = true,
            IsVisibleToClients = true,
            IsInherited = false,
            DisplayName = "Secret setting",
            Description = "A secret value",
        };

        def.DefaultValue.ShouldBe("default");
        def.IsEncrypted.ShouldBeTrue();
        def.IsVisibleToClients.ShouldBeTrue();
        def.IsInherited.ShouldBeFalse();
        def.DisplayName.ShouldBe("Secret setting");
        def.Description.ShouldBe("A secret value");
    }

    [Fact]
    public void Providers_CanBePopulated_ViaCollectionInitializer()
    {
        SettingDefinition def = new("App.Theme")
        {
            Providers = { "U", "G" },
        };

        def.Providers.Count.ShouldBe(2);
        def.Providers.ShouldContain("U");
        def.Providers.ShouldContain("G");
    }
}
