using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Granit.Identity.Local.Internal;
using Granit.Timing;

namespace Granit.Identity.Local.Services;

/// <summary>
/// Shared RFC 6238 TOTP implementation using <see cref="HMACSHA1"/> for two-factor authentication.
/// </summary>
/// <remarks>
/// Uses a +/-1 time-step window to tolerate clock drift between the server and authenticator apps
/// (RFC 6238 §5.2). Subclasses supply the issuer displayed in authenticator apps via
/// <see cref="Issuer"/>.
/// </remarks>
public abstract class TotpServiceBase(IClock clock) : ITotpService
{
    private const int KeyLength = 20; // 160-bit per RFC 4226 §4
    private const int TimeStepSeconds = 30;
    private const int CodeDigits = 6;
    private const int CodeModulus = 1_000_000; // 10^6

    /// <summary>
    /// The issuer label embedded in the <c>otpauth://</c> URI and displayed by authenticator apps.
    /// </summary>
    protected abstract string Issuer { get; }

    /// <inheritdoc/>
    public string GenerateSharedKey() => Base32.Encode(RandomNumberGenerator.GetBytes(KeyLength));

    /// <inheritdoc/>
    public string GetQrCodeUri(string email, string sharedKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(sharedKey);

        string encodedIssuer = Uri.EscapeDataString(Issuer);
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

        byte[] key = Base32.Decode(sharedKey);
        long currentStep = clock.Now.ToUnixTimeSeconds() / TimeStepSeconds;

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
#pragma warning disable CA5350 // Do not use weak cryptographic algorithms
    private static string ComputeTotp(byte[] key, long timeStep)
    {
        Span<byte> timeBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(timeBytes, timeStep);

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
}
