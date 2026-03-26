using Granit.Auditing.ConfigurationChanges.Internal;
using Shouldly;
using Xunit;

namespace Granit.Auditing.ConfigurationChanges.Tests.Internal;

public sealed class SensitiveValueMaskerTests
{
    [Theory]
    [InlineData("Smtp:Password")]
    [InlineData("OAuth:ClientSecret")]
    [InlineData("Database:ConnectionString")]
    [InlineData("External:ApiKey")]
    [InlineData("Auth:SigningKey")]
    [InlineData("Vault:EncryptionKey")]
    [InlineData("Hmac:HmacKey")]
    [InlineData("Service:PrivateKey")]
    [InlineData("Api:Token")]
    [InlineData("Azure:Credential")]
    public void MaskIfSensitive_WithSensitiveSettingName_ReturnsMask(string settingName)
    {
        string? result = SensitiveValueMasker.MaskIfSensitive(settingName, "my-secret-value");

        result.ShouldBe("***");
    }

    [Theory]
    [InlineData("App:Theme")]
    [InlineData("UI:PageSize")]
    [InlineData("Feature:DarkMode")]
    [InlineData("Logging:Level")]
    public void MaskIfSensitive_WithNonSensitiveSettingName_ReturnsOriginalValue(string settingName)
    {
        string? result = SensitiveValueMasker.MaskIfSensitive(settingName, "light");

        result.ShouldBe("light");
    }

    [Fact]
    public void MaskIfSensitive_WithNullValue_ReturnsNull()
    {
        string? result = SensitiveValueMasker.MaskIfSensitive("Smtp:Password", null);

        result.ShouldBeNull();
    }

    [Fact]
    public void MaskIfSensitive_IsCaseInsensitive()
    {
        string? result = SensitiveValueMasker.MaskIfSensitive("smtp:password", "secret");

        result.ShouldBe("***");
    }

    [Theory]
    [InlineData("postgres://admin:s3cret@db.example.com/mydb")]
    [InlineData("https://user:pass@api.example.com/v1")]
    [InlineData("amqp://guest:guest@rabbitmq:5672")]
    public void MaskIfSensitive_WithEmbeddedUrlCredentials_ReturnsMask(string value)
    {
        string? result = SensitiveValueMasker.MaskIfSensitive("App:DatabaseUrl", value);

        result.ShouldBe("***");
    }

    [Theory]
    [InlineData("Server=db;Database=app;Password=secret;")]
    [InlineData("Server=db;Pwd=secret;")]
    [InlineData("DefaultEndpointsProtocol=https;AccountKey=abc123;")]
    [InlineData("Endpoint=sb://bus.example.com;SharedAccessKey=xyz;")]
    public void MaskIfSensitive_WithEmbeddedConnectionStringCredentials_ReturnsMask(string value)
    {
        string? result = SensitiveValueMasker.MaskIfSensitive("App:SmtpConfig", value);

        result.ShouldBe("***");
    }

    [Theory]
    [InlineData("https://api.example.com/v1")]
    [InlineData("just a normal value")]
    [InlineData("Server=db;Database=app;Timeout=30;")]
    public void MaskIfSensitive_WithNonSensitiveNameAndSafeValue_ReturnsOriginalValue(string value)
    {
        string? result = SensitiveValueMasker.MaskIfSensitive("App:ServiceUrl", value);

        result.ShouldBe(value);
    }
}
