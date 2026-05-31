using System.Text.Json;
using Granit.DataExchange.Export;
using Granit.OpenIddict.Exports;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Exports;

public sealed class OpenIddictExportDefinitionTests
{
    // ---- Application -------------------------------------------------------

    private static readonly OpenIddictApplicationExportDefinition AppSut = new();
    private static IReadOnlyList<ExportFieldDescriptor> AppFields =>
        ((IExportDefinitionDescriptor)AppSut).GetFields();

    [Fact]
    public void Application_Name_is_stable() =>
        AppSut.Name.ShouldBe("Granit.OpenIddict.ApplicationExport");

    [Fact]
    public void Application_HasComplexFields_is_true() =>
        ((IExportDefinitionDescriptor)AppSut).HasComplexFields.ShouldBeTrue();

    [Fact]
    public void Application_OnIncompatibleField_default_is_Throw() =>
        ((IExportDefinitionDescriptor)AppSut).OnIncompatibleField.ShouldBe(OnIncompatibleFieldPolicy.Throw);

    [Theory]
    [InlineData("Permissions")]
    [InlineData("RedirectUris")]
    [InlineData("PostLogoutRedirectUris")]
    [InlineData("Requirements")]
    public void Application_array_field_has_RequiresHierarchy(string fieldName)
    {
        ExportFieldDescriptor field = AppFields.Single(f => f.PropertyPath == fieldName);
        field.RequiresHierarchy.ShouldBeTrue();
        field.ValueSelector.ShouldNotBeNull();
        field.SelectorType.ShouldBe(typeof(string[]));
    }

    [Theory]
    [InlineData("JsonWebKeySet")]
    [InlineData("Properties")]
    public void Application_json_bag_fields_are_scalar(string fieldName)
    {
        ExportFieldDescriptor field = AppFields.Single(f => f.PropertyPath == fieldName);
        field.RequiresHierarchy.ShouldBeFalse();
    }

    [Fact]
    public void Application_ClientSecret_is_not_exported() =>
        AppFields.ShouldNotContain(f =>
            f.PropertyPath.Contains("ClientSecret", StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Application_Permissions_selector_deserializes_json_array()
    {
        ExportFieldDescriptor field = AppFields.Single(f => f.PropertyPath == "Permissions");
        // Simulate the raw JSON string OpenIddict stores in the DB
        string permissionsJson = JsonSerializer.Serialize(new[] { "ept:token", "gt:authorization_code" });
        // Build a minimal fake entity using a dynamic object approach via reflection
        var app = new Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication();
        typeof(Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication)
            .GetProperty("Permissions")!
            .SetValue(app, permissionsJson);

        field.ValueSelector!(app).ShouldBeOfType<string[]>()
            .ShouldBe(["ept:token", "gt:authorization_code"]);
    }

    [Fact]
    public void Application_Permissions_selector_returns_empty_array_when_null()
    {
        ExportFieldDescriptor field = AppFields.Single(f => f.PropertyPath == "Permissions");
        var app = new Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictApplication();

        field.ValueSelector!(app).ShouldBeOfType<string[]>().ShouldBeEmpty();
    }

    // ---- Scope -------------------------------------------------------------

    private static readonly OpenIddictScopeExportDefinition ScopeSut = new();
    private static IReadOnlyList<ExportFieldDescriptor> ScopeFields =>
        ((IExportDefinitionDescriptor)ScopeSut).GetFields();

    [Fact]
    public void Scope_Name_is_stable() =>
        ScopeSut.Name.ShouldBe("Granit.OpenIddict.ScopeExport");

    [Fact]
    public void Scope_HasComplexFields_is_true() =>
        ((IExportDefinitionDescriptor)ScopeSut).HasComplexFields.ShouldBeTrue();

    [Fact]
    public void Scope_Resources_field_has_RequiresHierarchy()
    {
        ExportFieldDescriptor field = ScopeFields.Single(f => f.PropertyPath == "Resources");
        field.RequiresHierarchy.ShouldBeTrue();
        field.SelectorType.ShouldBe(typeof(string[]));
    }

    [Fact]
    public void Scope_Properties_is_scalar()
    {
        ExportFieldDescriptor field = ScopeFields.Single(f => f.PropertyPath == "Properties");
        field.RequiresHierarchy.ShouldBeFalse();
    }

    [Fact]
    public void Scope_Resources_selector_deserializes_json_array()
    {
        ExportFieldDescriptor field = ScopeFields.Single(f => f.PropertyPath == "Resources");
        string resourcesJson = JsonSerializer.Serialize(new[] { "api://granit", "api://business" });
        var scope = new Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope();
        typeof(Granit.OpenIddict.Entities.OpenIddict.GranitOpenIddictScope)
            .GetProperty("Resources")!
            .SetValue(scope, resourcesJson);

        field.ValueSelector!(scope).ShouldBeOfType<string[]>()
            .ShouldBe(["api://granit", "api://business"]);
    }
}
