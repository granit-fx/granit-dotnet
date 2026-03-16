using Granit.Encryption.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Encryption.EntityFrameworkCore.Tests;

public sealed class EncryptedStringConverterTests
{
    private readonly IStringEncryptionService _encryption = Substitute.For<IStringEncryptionService>();
    private readonly EncryptedStringConverter _sut;

    public EncryptedStringConverterTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(ci => $"ENC:{ci.Arg<string>()}");
        _encryption.Decrypt(Arg.Any<string>()).Returns(ci =>
        {
            string s = ci.Arg<string>();
            return s.StartsWith("ENC:", StringComparison.Ordinal) ? s["ENC:".Length..] : null;
        });

        _sut = new EncryptedStringConverter(_encryption);
    }

    [Fact]
    public void ConvertToProvider_Encrypts_PlainText()
    {
        Func<string, string> toProvider = _sut.ConvertToProviderExpression.Compile();

        toProvider("secret").ShouldBe("ENC:secret");
    }

    [Fact]
    public void ConvertFromProvider_Decrypts_CipherText()
    {
        Func<string, string> fromProvider = _sut.ConvertFromProviderExpression.Compile();

        fromProvider("ENC:secret").ShouldBe("secret");
    }

    [Fact]
    public void ConvertFromProvider_ReturnsEmpty_WhenDecryptFails()
    {
        _encryption.Decrypt("bad-cipher").Returns((string?)null);
        Func<string, string> fromProvider = _sut.ConvertFromProviderExpression.Compile();

        fromProvider("bad-cipher").ShouldBe(string.Empty);
    }

    [Fact]
    public void RoundTrip_PlainText_IsPreserved()
    {
        Func<string, string> toProvider = _sut.ConvertToProviderExpression.Compile();
        Func<string, string> fromProvider = _sut.ConvertFromProviderExpression.Compile();

        fromProvider(toProvider("hello world")).ShouldBe("hello world");
    }
}
