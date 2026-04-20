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

    // -------------------------------------------------------------------------
    // ValueKind + AllowedValues defaults
    // -------------------------------------------------------------------------

    [Fact]
    public void ValueKind_IsString_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.ValueKind.ShouldBe(ValueKind.String);
    }

    [Fact]
    public void AllowedValues_IsNull_ByDefault()
    {
        SettingDefinition def = new("App.Theme");

        def.AllowedValues.ShouldBeNull();
    }

    [Fact]
    public void ValueKind_And_AllowedValues_CanBeSet_ViaInitSyntax()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            ValueKind = ValueKind.String,
            AllowedValues = ["Debug", "Information", "Warning", "Error"],
            DefaultValue = "Information",
        };

        def.ValueKind.ShouldBe(ValueKind.String);
        def.AllowedValues.ShouldNotBeNull();
        def.AllowedValues.Count.ShouldBe(4);
        def.AllowedValues.ShouldContain("Debug");
    }

    // -------------------------------------------------------------------------
    // ValidateInvariants — DefaultValue parsing per ValueKind
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(ValueKind.Bool, "true")]
    [InlineData(ValueKind.Bool, "false")]
    [InlineData(ValueKind.Bool, "True")]
    [InlineData(ValueKind.Int, "0")]
    [InlineData(ValueKind.Int, "-42")]
    [InlineData(ValueKind.Int, "2147483647")]
    [InlineData(ValueKind.Double, "3.14")]
    [InlineData(ValueKind.Double, "-1e-5")]
    [InlineData(ValueKind.Double, "0")]
    [InlineData(ValueKind.Json, "{\"key\":\"value\"}")]
    [InlineData(ValueKind.Json, "[1,2,3]")]
    [InlineData(ValueKind.Json, "\"hello\"")]
    [InlineData(ValueKind.Json, "null")]
    [InlineData(ValueKind.String, "anything goes")]
    public void ValidateInvariants_ParseableDefaultValue_DoesNotThrow(ValueKind kind, string defaultValue)
    {
        SettingDefinition def = new("App.X")
        {
            ValueKind = kind,
            DefaultValue = defaultValue,
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    [Theory]
    [InlineData(ValueKind.Bool, "yes")]
    [InlineData(ValueKind.Bool, "1")]
    [InlineData(ValueKind.Int, "3.14")]
    [InlineData(ValueKind.Int, "abc")]
    [InlineData(ValueKind.Double, "not-a-number")]
    [InlineData(ValueKind.Json, "{malformed")]
    [InlineData(ValueKind.Json, "")]
    public void ValidateInvariants_NonParseableDefaultValue_Throws(ValueKind kind, string defaultValue)
    {
        SettingDefinition def = new("App.X")
        {
            ValueKind = kind,
            DefaultValue = defaultValue,
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => def.ValidateInvariants());
        ex.Message.ShouldContain("App.X");
        ex.Message.ShouldContain("DefaultValue");
    }

    [Fact]
    public void ValidateInvariants_NullDefaultValue_DoesNotThrow()
    {
        SettingDefinition def = new("App.X")
        {
            ValueKind = ValueKind.Int,
            DefaultValue = null,
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    // -------------------------------------------------------------------------
    // ValidateInvariants — AllowedValues
    // -------------------------------------------------------------------------

    [Fact]
    public void ValidateInvariants_DefaultValueInAllowedValues_DoesNotThrow()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            AllowedValues = ["Debug", "Information", "Warning"],
            DefaultValue = "Information",
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariants_DefaultValueNotInAllowedValues_Throws()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            AllowedValues = ["Debug", "Information", "Warning"],
            DefaultValue = "Trace",
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => def.ValidateInvariants());
        ex.Message.ShouldContain("App.LogLevel");
        ex.Message.ShouldContain("AllowedValues");
    }

    [Fact]
    public void ValidateInvariants_NullDefaultValue_WithAllowedValues_DoesNotThrow()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            AllowedValues = ["Debug", "Information"],
            DefaultValue = null,
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariants_EmptyAllowedValues_DoesNotConstrainDefaultValue()
    {
        SettingDefinition def = new("App.X")
        {
            AllowedValues = [],
            DefaultValue = "anything",
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariants_AllowedValuesOnIntKind_AcceptsStringIntegers()
    {
        SettingDefinition def = new("App.MaxRetries")
        {
            ValueKind = ValueKind.Int,
            AllowedValues = ["1", "5", "10"],
            DefaultValue = "5",
        };

        Should.NotThrow(() => def.ValidateInvariants());
    }

    [Fact]
    public void ValidateInvariants_AllowedValuesOnIntKind_RejectsNonIntegerEntry()
    {
        SettingDefinition def = new("App.MaxRetries")
        {
            ValueKind = ValueKind.Int,
            AllowedValues = ["1", "5", "ten"],
        };

        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() => def.ValidateInvariants());
        ex.Message.ShouldContain("App.MaxRetries");
        ex.Message.ShouldContain("AllowedValues");
        ex.Message.ShouldContain("Int");
    }

    // -------------------------------------------------------------------------
    // IsValidValue
    // -------------------------------------------------------------------------

    [Fact]
    public void IsValidValue_NullValue_IsAlwaysValid()
    {
        SettingDefinition def = new("App.X")
        {
            ValueKind = ValueKind.Int,
            AllowedValues = ["1", "5"],
        };

        def.IsValidValue(null).ShouldBeTrue();
    }

    [Theory]
    [InlineData(ValueKind.Bool, "true", true)]
    [InlineData(ValueKind.Bool, "maybe", false)]
    [InlineData(ValueKind.Int, "42", true)]
    [InlineData(ValueKind.Int, "3.14", false)]
    [InlineData(ValueKind.Double, "3.14", true)]
    [InlineData(ValueKind.Json, "{\"a\":1}", true)]
    [InlineData(ValueKind.Json, "{malformed", false)]
    public void IsValidValue_ChecksValueKindParseability(ValueKind kind, string value, bool expected)
    {
        SettingDefinition def = new("App.X") { ValueKind = kind };

        def.IsValidValue(value).ShouldBe(expected);
    }

    [Fact]
    public void IsValidValue_WithAllowedValues_RejectsValuesOutsideList()
    {
        SettingDefinition def = new("App.LogLevel")
        {
            AllowedValues = ["Debug", "Information", "Warning"],
        };

        def.IsValidValue("Information").ShouldBeTrue();
        def.IsValidValue("Trace").ShouldBeFalse();
    }

    [Fact]
    public void IsValidValue_EmptyAllowedValues_AcceptsAnyParseableValue()
    {
        SettingDefinition def = new("App.X")
        {
            ValueKind = ValueKind.Int,
            AllowedValues = [],
        };

        def.IsValidValue("42").ShouldBeTrue();
        def.IsValidValue("abc").ShouldBeFalse();
    }
}
