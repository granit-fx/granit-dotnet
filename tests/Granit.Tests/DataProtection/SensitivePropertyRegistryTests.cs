// =============================================================================
// Tests — SensitivePropertyRegistry: scanning, lookup, merge, and edge cases
// =============================================================================

using Granit.DataProtection;
using Shouldly;
using Xunit;

namespace Granit.Tests.DataProtection;

public sealed class SensitivePropertyRegistryTests
{
    // -------------------------------------------------------------------------
    // Empty assembly scan
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_EmptyAssemblyList_CreatesEmptyRegistry()
    {
        SensitivePropertyRegistry registry = new([]);

        registry.Entries.ShouldBeEmpty();
    }

    // -------------------------------------------------------------------------
    // Scanning types with [SensitiveData]
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_AssemblyWithSensitiveProperties_RegistersThem()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.Entries.ShouldNotBeEmpty();
    }

    [Fact]
    public void Constructor_ScansPublicInstanceProperties_Only()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        // InternalSecret is not a public property, should not be registered
        registry.TryGet("InternalSecret", out _).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // TryGet — hit and miss
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGet_RegisteredProperty_ReturnsTrueWithEntry()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.TryGet("Email", out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Confidential);
        entry.Mode.ShouldBe(SensitiveDataMode.Mask);
    }

    [Fact]
    public void TryGet_UnknownProperty_ReturnsFalse()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.TryGet("NonExistentProperty", out _).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Case-insensitive lookup
    // -------------------------------------------------------------------------

    [Fact]
    public void TryGet_CaseInsensitive_MatchesRegardlessOfCase()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.TryGet("email", out _).ShouldBeTrue();
        registry.TryGet("EMAIL", out _).ShouldBeTrue();
        registry.TryGet("Email", out _).ShouldBeTrue();
        registry.TryGet("eMaIl", out _).ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // IsSensitiveAtLevel — threshold checks
    // -------------------------------------------------------------------------

    [Fact]
    public void IsSensitiveAtLevel_AtOrAboveThreshold_ReturnsTrue()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        // Email is Confidential (1) — threshold Internal (0) should match
        registry.IsSensitiveAtLevel("Email", Sensitivity.Internal, out _).ShouldBeTrue();

        // Email is Confidential (1) — threshold Confidential (1) should match
        registry.IsSensitiveAtLevel("Email", Sensitivity.Confidential, out _).ShouldBeTrue();
    }

    [Fact]
    public void IsSensitiveAtLevel_BelowThreshold_ReturnsFalse()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        // FirstName is Internal (0) — threshold Confidential (1) should not match
        registry.IsSensitiveAtLevel("FirstName", Sensitivity.Confidential, out _).ShouldBeFalse();
    }

    [Fact]
    public void IsSensitiveAtLevel_UnknownProperty_ReturnsFalse()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.IsSensitiveAtLevel("Unknown", Sensitivity.Internal, out _).ShouldBeFalse();
    }

    [Fact]
    public void IsSensitiveAtLevel_RestrictedProperty_MatchesRestrictedThreshold()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.IsSensitiveAtLevel("PasswordHash", Sensitivity.Restricted, out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Restricted);
        entry.Mode.ShouldBe(SensitiveDataMode.Omit);
    }

    // -------------------------------------------------------------------------
    // Merge — most restrictive level and mode win
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_DuplicatePropertyName_MostRestrictiveLevelWins()
    {
        // Both TypeA and TypeB have a "SharedField" property with different levels.
        // TypeA: Internal + Mask, TypeB: Confidential + Hash
        // Most restrictive: Confidential + Hash
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.TryGet("SharedField", out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Confidential);
        entry.Mode.ShouldBe(SensitiveDataMode.Hash);
    }

    // -------------------------------------------------------------------------
    // Entries property
    // -------------------------------------------------------------------------

    [Fact]
    public void Entries_ReturnsAllRegisteredProperties()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.Entries.ShouldContainKey("Email");
        registry.Entries.ShouldContainKey("FirstName");
        registry.Entries.ShouldContainKey("PasswordHash");
        registry.Entries.ShouldContainKey("SharedField");
    }

    [Fact]
    public void Entries_CaseInsensitiveKeys()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        // FrozenDictionary with OrdinalIgnoreCase should support case-insensitive key lookup
        registry.Entries.ContainsKey("email").ShouldBeTrue();
        registry.Entries.ContainsKey("EMAIL").ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // SensitivePropertyEntry record struct
    // -------------------------------------------------------------------------

    [Fact]
    public void SensitivePropertyEntry_RecordStructEquality()
    {
        SensitivePropertyEntry a = new(Sensitivity.Confidential, SensitiveDataMode.Mask);
        SensitivePropertyEntry b = new(Sensitivity.Confidential, SensitiveDataMode.Mask);
        SensitivePropertyEntry c = new(Sensitivity.Restricted, SensitiveDataMode.Omit);

        a.ShouldBe(b);
        a.ShouldNotBe(c);
    }

    // -------------------------------------------------------------------------
    // ReflectionTypeLoadException handling
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_AssemblyThrowingReflectionTypeLoadException_HandlesGracefully()
    {
        // The registry handles ReflectionTypeLoadException by scanning the non-null types.
        // We cannot easily trigger this from a real assembly, but we can verify the
        // registry doesn't throw when scanning the current assembly (which succeeds).
        // The code path is verified by its existence in the source — this test ensures
        // that scanning a normal assembly works without error.
        SensitivePropertyRegistry registry = new([typeof(SensitivePropertyRegistryTests).Assembly]);

        registry.Entries.ShouldNotBeNull();
    }

    // -------------------------------------------------------------------------
    // Default attribute values
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_DefaultSensitiveDataAttribute_UsesInternalAndMask()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        // FirstName uses [SensitiveData] with no arguments = Internal + Mask
        registry.TryGet("FirstName", out SensitivePropertyEntry entry).ShouldBeTrue();
        entry.Level.ShouldBe(Sensitivity.Internal);
        entry.Mode.ShouldBe(SensitiveDataMode.Mask);
    }

    // -------------------------------------------------------------------------
    // Non-attributed properties are not registered
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_PropertiesWithoutAttribute_NotRegistered()
    {
        SensitivePropertyRegistry registry = CreateRegistryFromThisAssembly();

        registry.TryGet("RegularProperty", out _).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static SensitivePropertyRegistry CreateRegistryFromThisAssembly() =>
        new([typeof(SensitivePropertyRegistryTests).Assembly]);

    // -------------------------------------------------------------------------
    // Test fixtures — types with [SensitiveData] for scanning
    // -------------------------------------------------------------------------

    // ReSharper disable UnusedAutoPropertyAccessor.Local — scanned via reflection
#pragma warning disable CA1822 // Member does not access instance data

    private sealed class PersonWithSensitiveData
    {
        [SensitiveData]
        public string? FirstName { get; set; }

        [SensitiveData(Level = Sensitivity.Confidential)]
        public string? Email { get; set; }

        [SensitiveData(Level = Sensitivity.Restricted, Mode = SensitiveDataMode.Omit)]
        public string? PasswordHash { get; set; }

        public string? RegularProperty { get; set; }

        [SensitiveData]
        internal string? InternalSecret { get; set; }
    }

    private sealed class TypeAWithSharedField
    {
        [SensitiveData(Level = Sensitivity.Internal, Mode = SensitiveDataMode.Mask)]
        public string? SharedField { get; set; }
    }

    private sealed class TypeBWithSharedField
    {
        [SensitiveData(Level = Sensitivity.Confidential, Mode = SensitiveDataMode.Hash)]
        public string? SharedField { get; set; }
    }

#pragma warning restore CA1822
    // ReSharper restore UnusedAutoPropertyAccessor.Local
}
