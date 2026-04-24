// =============================================================================
// Tests - ValidationErrorsSanitizer (VULN-203)
// =============================================================================
// Verifies that ProblemDetails.Extensions["errors"] redacts messages for
// fields marked [SensitiveData(Level >= Confidential)]. Property paths
// support top-level fields, nested objects ("Address.Cvv"), and
// collection indexing ("Users[0].Password", "Items[3].Card.Cvv").
// Purely-numeric path segments are skipped (they are collection indices,
// not property names).
// =============================================================================

using System.Reflection;
using Granit.DataProtection;
using Granit.Http.ExceptionHandling.Internal;
using Shouldly;
using Xunit;

namespace Granit.Http.ExceptionHandling.Tests;

public sealed class ValidationErrorsSanitizerTests
{
    // Fixture types with sensitive property declarations scanned by the registry.
    private sealed class PaymentFixture
    {
        public string? Amount { get; set; }

        [SensitiveData(Level = Sensitivity.Restricted)]
        public string? Cvv { get; set; }

        [SensitiveData(Level = Sensitivity.Restricted)]
        public string? Password { get; set; }

        [SensitiveData(Level = Sensitivity.Confidential)]
        public string? SecretCode { get; set; }

        // Internal sensitivity < Confidential threshold — must NOT be redacted.
        [SensitiveData(Level = Sensitivity.Internal)]
        public string? FirstName { get; set; }
    }

    private static ValidationErrorsSanitizer BuildSanitizer() =>
        new(new SensitivePropertyRegistry([typeof(PaymentFixture).Assembly]));

    // -------------------------------------------------------------------------
    // Top-level fields
    // -------------------------------------------------------------------------

    [Fact]
    public void Sanitize_TopLevel_SensitiveField_ReplacesMessage()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Cvv"] = ["'Cvv' must equal '1234'"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Cvv"].ShouldBe(["Invalid value."]);
        result["Cvv"][0].ShouldNotContain("1234");
    }

    [Fact]
    public void Sanitize_TopLevel_NonSensitiveField_IsUnchanged()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Amount"] = ["'Amount' must be greater than 0"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Amount"].ShouldBe(["'Amount' must be greater than 0"]);
    }

    [Fact]
    public void Sanitize_InternalSensitivity_BelowConfidentialThreshold_IsUnchanged()
    {
        // FirstName is [SensitiveData(Level = Internal)] — below the
        // Confidential threshold the sanitizer uses, so it stays intact.
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["FirstName"] = ["'FirstName' must not be empty"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["FirstName"].ShouldBe(["'FirstName' must not be empty"]);
    }

    // -------------------------------------------------------------------------
    // Nested property paths
    // -------------------------------------------------------------------------

    [Fact]
    public void Sanitize_NestedPath_SensitiveLeaf_IsRedacted()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Address.SecretCode"] = ["'SecretCode' 'abc-xyz' is invalid"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Address.SecretCode"].ShouldBe(["Invalid value."]);
        result["Address.SecretCode"][0].ShouldNotContain("abc-xyz");
    }

    [Fact]
    public void Sanitize_CollectionIndex_NumericSegmentIsSkipped_SensitiveLeafRedacted()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Users[0].Password"] = ["'Password' must equal 'hunter2'"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Users[0].Password"].ShouldBe(["Invalid value."]);
        result["Users[0].Password"][0].ShouldNotContain("hunter2");
    }

    [Fact]
    public void Sanitize_DeepNesting_SensitiveLeafRedacted()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Items[3].Card.Cvv"] = ["'Cvv' '987' is invalid"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Items[3].Card.Cvv"].ShouldBe(["Invalid value."]);
        result["Items[3].Card.Cvv"][0].ShouldNotContain("987");
    }

    [Fact]
    public void Sanitize_RootPathWithoutSensitiveSegment_NoFalsePositive()
    {
        // "Address" alone is not sensitive; only "Address.Cvv" or similar would be.
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Address"] = ["'Address' must not be empty"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Address"].ShouldBe(["'Address' must not be empty"]);
    }

    // -------------------------------------------------------------------------
    // Mixed payloads
    // -------------------------------------------------------------------------

    [Fact]
    public void Sanitize_MixedPayload_OnlySensitiveKeysRedacted()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Amount"] = ["Amount must be > 0"],
            ["Cvv"] = ["Cvv 'secret' is invalid"],
            ["Users[0].Password"] = ["Password 'hunter2' is weak"],
            ["Email"] = ["Email format invalid"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Amount"].ShouldBe(["Amount must be > 0"]);
        result["Cvv"].ShouldBe(["Invalid value."]);
        result["Users[0].Password"].ShouldBe(["Invalid value."]);
        result["Email"].ShouldBe(["Email format invalid"]); // Email not declared on fixture
    }

    // -------------------------------------------------------------------------
    // Edge cases
    // -------------------------------------------------------------------------

    [Fact]
    public void Sanitize_EmptyDictionary_ReturnsInputInstance()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = [];

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result.Count.ShouldBe(0);
    }

    [Fact]
    public void Sanitize_NoSensitiveKeys_ReturnsOriginalInstance()
    {
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["Amount"] = ["too small"],
            ["Quantity"] = ["too large"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        // Performance invariant: no allocation when nothing to redact.
        result.ShouldBeSameAs(errors);
    }

    [Fact]
    public void Sanitize_NullRegistry_NoOp()
    {
        // Absence of registry (e.g. Granit.DataProtection not wired) must not
        // break the exception handler — sanitization becomes a no-op pass-through.
        ValidationErrorsSanitizer sanitizer = new(registry: null);
        Dictionary<string, string[]> errors = new()
        {
            ["Cvv"] = ["Cvv 'secret' is invalid"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["Cvv"].ShouldBe(["Cvv 'secret' is invalid"]);
    }

    [Fact]
    public void Sanitize_CaseInsensitiveMatch_RedactsEvenWithDifferentCasing()
    {
        // Registry uses OrdinalIgnoreCase; ModelState may produce paths with
        // different casing than the declared property name.
        ValidationErrorsSanitizer sanitizer = BuildSanitizer();
        Dictionary<string, string[]> errors = new()
        {
            ["users[0].password"] = ["password 'abc' invalid"],
        };

        IReadOnlyDictionary<string, string[]> result = sanitizer.Sanitize(errors);

        result["users[0].password"].ShouldBe(["Invalid value."]);
    }
}
