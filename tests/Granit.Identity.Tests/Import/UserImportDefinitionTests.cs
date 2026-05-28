using Granit.DataExchange.Import;
using Granit.Identity.Domain;
using Granit.Identity.Import;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Import;

public sealed class UserImportDefinitionTests
{
    [Fact]
    public void Name_IsCanonicalIdentifier()
    {
        var definition = new UserImportDefinition();

        definition.Name.ShouldBe("Granit.Identity.UserImport");
    }

    [Fact]
    public void EntityType_IsUser()
    {
        var definition = new UserImportDefinition();

        definition.EntityType.ShouldBe(typeof(User));
    }

    [Fact]
    public void BusinessKey_IsEmail()
    {
        var definition = new UserImportDefinition();

        IReadOnlyList<string> keys = definition.GetBusinessKeyProperties();

        keys.ShouldBe(["Email"]);
    }

    [Fact]
    public void Properties_AreProfileWhitelistInOrder()
    {
        var definition = new UserImportDefinition();

        string[] expected =
        [
            nameof(User.Email),
            nameof(User.DisplayName),
            nameof(User.FirstName),
            nameof(User.LastName),
            nameof(User.PhoneNumber),
            nameof(User.PreferredLocale),
            nameof(User.Timezone),
            nameof(User.IsEnabled),
        ];

        IReadOnlyList<PropertyMapping> properties = definition.GetProperties();

        properties.Select(p => p.PropertyPath).ShouldBe(expected);
    }

    [Fact]
    public void Email_IsRequired()
    {
        var definition = new UserImportDefinition();

        PropertyMapping email = definition.GetProperties()
            .Single(p => p.PropertyPath == nameof(User.Email));

        email.IsRequired.ShouldBeTrue();
        email.DisplayName.ShouldBe("Email");
    }

    [Fact]
    public void DisplayName_IsRequired()
    {
        var definition = new UserImportDefinition();

        PropertyMapping displayName = definition.GetProperties()
            .Single(p => p.PropertyPath == nameof(User.DisplayName));

        displayName.IsRequired.ShouldBeTrue();
        displayName.DisplayName.ShouldBe("Name");
    }

    [Theory]
    [InlineData("PasswordHash")]
    [InlineData("SecurityStamp")]
    [InlineData("EmailHash")]
    [InlineData("PhoneNumberHash")]
    [InlineData("TenantId")]
    [InlineData("Id")]
    public void SensitiveProperties_AreNotImportable(string propertyName)
    {
        var definition = new UserImportDefinition();

        definition.GetProperties()
            .ShouldNotContain(p => p.PropertyPath == propertyName);
    }
}
