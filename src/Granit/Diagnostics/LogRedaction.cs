using System.Security.Cryptography;
using System.Text;

namespace Granit.Diagnostics;

/// <summary>
/// PII redaction helpers for structured logging and OpenTelemetry span attributes.
/// Use these methods to prevent sensitive data (email, phone, IP, device tokens)
/// from reaching observability backends (GDPR Art. 5, ISO 27001 A.5.34).
/// </summary>
public static class LogRedaction
{
    private const string Mask = "***";

    /// <summary>
    /// Redacts an email address, preserving a prefix and the domain for debugging.
    /// <example><c>"john.doe@example.com"</c> → <c>"joh***@example.com"</c></example>
    /// </summary>
    public static string Email(string email)
    {
        int atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return Mask;
        }

        int keep = Math.Min(3, atIndex);
        return string.Concat(email.AsSpan(0, keep), Mask, email.AsSpan(atIndex));
    }

    /// <summary>
    /// Extracts the domain part of an email address. Returns a bounded, non-PII value
    /// suitable as a trace span tag.
    /// <example><c>"john.doe@example.com"</c> → <c>"example.com"</c></example>
    /// </summary>
    public static string EmailDomain(string email)
    {
        int atIndex = email.IndexOf('@');
        return atIndex >= 0 ? email[(atIndex + 1)..] : "unknown";
    }

    /// <summary>
    /// Redacts a phone number, preserving the country code prefix and last 2 digits.
    /// <example><c>"+33612345678"</c> → <c>"+336*****78"</c></example>
    /// </summary>
    public static string Phone(string phone)
    {
        if (phone.Length <= 4)
        {
            return Mask;
        }

        int prefixKeep = Math.Min(4, phone.Length - 2);
        return $"{phone.AsSpan(0, prefixKeep)}*****{phone.AsSpan(phone.Length - 2)}";
    }

    /// <summary>
    /// Redacts a device token or opaque token, preserving a short prefix and suffix
    /// for log correlation.
    /// <example><c>"dLkj3FDmAbCdEfGh"</c> → <c>"dLkj...fGh"</c></example>
    /// </summary>
    public static string Token(string token)
    {
        if (token.Length <= 8)
        {
            return Mask;
        }

        return $"{token.AsSpan(0, 4)}...{token.AsSpan(token.Length - 3)}";
    }

    /// <summary>
    /// Masks an IPv4 address to /24 (last octet replaced).
    /// IPv6 addresses are truncated after the fourth group.
    /// <example><c>"192.168.1.42"</c> → <c>"192.168.1.***"</c></example>
    /// </summary>
    public static string IpAddress(string ip)
    {
        int lastDot = ip.LastIndexOf('.');
        if (lastDot > 0)
        {
            return string.Concat(ip.AsSpan(0, lastDot + 1), Mask);
        }

        int colonCount = 0;
        for (int i = 0; i < ip.Length; i++)
        {
            if (ip[i] == ':')
            {
                colonCount++;
            }

            if (colonCount == 4)
            {
                return $"{ip.AsSpan(0, i)}:***";
            }
        }

        return Mask;
    }

    /// <summary>
    /// Redacts a username, preserving a short prefix for debugging.
    /// <example><c>"john_admin"</c> → <c>"joh***"</c></example>
    /// </summary>
    public static string Username(string username)
    {
        if (username.Length <= 3)
        {
            return Mask;
        }

        return string.Concat(username.AsSpan(0, 3), Mask);
    }

    /// <summary>
    /// Produces a short, non-reversible hash prefix for a value. Useful as a
    /// trace span tag when full content must not be stored but correlation is needed.
    /// <example><c>"john.doe@example.com"</c> → <c>"a1b2c3d4"</c> (8-char hex)</example>
    /// </summary>
    public static string HashPrefix(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash.AsSpan(0, 4));
    }
}
