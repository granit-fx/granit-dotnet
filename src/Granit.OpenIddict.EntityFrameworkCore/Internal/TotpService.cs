using System.Security.Cryptography;
using System.Text;
using Granit.OpenIddict.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// RFC 6238 TOTP implementation using <see cref="HMACSHA1"/> for two-factor authentication.
/// </summary>
/// <remarks>
/// Uses a ±1 time-step window to tolerate clock drift between the server and authenticator apps.
/// The issuer displayed in authenticator apps is read from
/// <see cref="TokenOptions.AuthenticatorIssuer"/>.
/// </remarks>
internal sealed class TotpService(
    IClock clock,
    IOptions<IdentityOptions> identityOptions) : ITotpService
{
    private const int KeyLength = 20; // 160-bit per RFC 4226 §4
    private const int TimeStepSeconds = 30;
    private const int CodeDigits = 6;
    private const int CodeModulus = 1_000_000; // 10^6
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <inheritdoc/>
    public string GenerateSharedKey()
    {
        byte[] key = RandomNumberGenerator.GetBytes(KeyLength);
        return Base32Encode(key);
    }

    /// <inheritdoc/>
    public string GetQrCodeUri(string email, string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        string issuer = identityOptions.Value.Tokens.AuthenticatorIssuer ?? "Granit";
        string encodedIssuer = Uri.EscapeDataString(issuer);
        string encodedEmail = Uri.EscapeDataString(email);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={sharedKey}&issuer={encodedIssuer}&digits={CodeDigits}&period={TimeStepSeconds}";
    }

    /// <inheritdoc/>
    public bool ValidateCode(string sharedKey, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        if (string.IsNullOrEmpty(code) || code.Length != CodeDigits)
        {
            return false;
        }

        byte[] key = Base32Decode(sharedKey);
        long currentStep = clock.Now.ToUnixTimeSeconds() / TimeStepSeconds;

        // Allow ±1 step for clock drift (RFC 6238 §5.2)
        for (long offset = -1; offset <= 1; offset++)
        {
            string computed = ComputeTotp(key, currentStep + offset);

            if (CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    // HMAC-SHA1 is mandated by RFC 6238 §5.1 / RFC 4226 §5 for interoperability
    // with authenticator apps (Google Authenticator, Microsoft Authenticator, etc.).
    // Upgrading to SHA-256/512 would break all existing TOTP clients.
#pragma warning disable CA5350 // Do not use weak cryptographic algorithms
    private static string ComputeTotp(byte[] key, long timeStep)
    {
        Span<byte> timeBytes = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(timeBytes, timeStep);

        Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
        HMACSHA1.HashData(key, timeBytes, hash);

        int offset = hash[^1] & 0x0F;
        int binaryCode = ((hash[offset] & 0x7F) << 24)
                       | ((hash[offset + 1] & 0xFF) << 16)
                       | ((hash[offset + 2] & 0xFF) << 8)
                       | (hash[offset + 3] & 0xFF);

        int otp = binaryCode % CodeModulus;
        return otp.ToString().PadLeft(CodeDigits, '0');
    }
#pragma warning restore CA5350

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var result = new StringBuilder((data.Length * 8 + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Base32Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Base32Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return result.ToString();
    }

    private static byte[] Base32Decode(string base32)
    {
        ReadOnlySpan<char> trimmed = base32.AsSpan().TrimEnd('=');
        byte[] result = new byte[trimmed.Length * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (char c in trimmed)
        {
            int value = char.ToUpperInvariant(c) switch
            {
                >= 'A' and <= 'Z' => c - 'A',
                >= '2' and <= '7' => c - '2' + 26,
                _ => throw new FormatException($"Invalid Base32 character: '{c}'.")
            };

            buffer = (buffer << 5) | value;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                result[index++] = (byte)(buffer >> bitsLeft);
            }
        }

        return result;
    }
}
