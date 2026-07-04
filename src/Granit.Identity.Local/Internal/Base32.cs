using System.Text;

namespace Granit.Identity.Local.Internal;

internal static class Base32
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var result = new StringBuilder(((data.Length * 8) + 4) / 5);
        int buffer = 0;
        int bitsLeft = 0;

        foreach (byte b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;

            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                result.Append(Alphabet[(buffer >> bitsLeft) & 0x1F]);
            }
        }

        if (bitsLeft > 0)
        {
            result.Append(Alphabet[(buffer << (5 - bitsLeft)) & 0x1F]);
        }

        return result.ToString();
    }

    public static byte[] Decode(string base32)
    {
        ArgumentNullException.ThrowIfNull(base32);

        ReadOnlySpan<char> trimmed = base32.AsSpan().TrimEnd('=');
        byte[] result = new byte[trimmed.Length * 5 / 8];
        int buffer = 0;
        int bitsLeft = 0;
        int index = 0;

        foreach (char c in trimmed)
        {
            char upper = char.ToUpperInvariant(c);
            int value = upper switch
            {
                >= 'A' and <= 'Z' => upper - 'A',
                >= '2' and <= '7' => upper - '2' + 26,
                _ => throw new FormatException($"Invalid Base32 character: '{c}'."),
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
