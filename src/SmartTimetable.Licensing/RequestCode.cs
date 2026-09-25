using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace SmartTimetable.Licensing;

/// <summary>
/// The human-friendly "request code" a customer emails to the vendor,
/// e.g. <c>ABCDE-FGHJK-...</c>. It encodes the machine fingerprint plus a CRC to catch typos.
/// </summary>
public static class RequestCode
{
    private const byte FormatVersion = 1;

    public static string Encode(MachineFingerprint fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);

        var body = fingerprint.ToBytes();
        var buffer = new byte[1 + body.Length + 2];
        buffer[0] = FormatVersion;
        body.CopyTo(buffer, 1);

        var crc = Crc16.Compute(buffer.AsSpan(0, buffer.Length - 2));
        buffer[^2] = (byte)(crc >> 8);
        buffer[^1] = (byte)(crc & 0xFF);

        return Group(Base32.Encode(buffer));
    }

    public static bool TryDecode(string? code, [NotNullWhen(true)] out MachineFingerprint? fingerprint)
    {
        fingerprint = null;
        if (string.IsNullOrWhiteSpace(code)) return false;
        if (!Base32.TryDecode(Clean(code), out var buffer) || buffer.Length < 4) return false;
        if (buffer[0] != FormatVersion) return false;

        var expected = Crc16.Compute(buffer.AsSpan(0, buffer.Length - 2));
        var actual = (ushort)((buffer[^2] << 8) | buffer[^1]);
        if (expected != actual) return false;

        return MachineFingerprint.TryFromBytes(buffer.AsSpan(1, buffer.Length - 3), out fingerprint);
    }

    /// <summary>Upper-cases and strips dashes/spaces so pasted codes are forgiving.</summary>
    public static string Clean(string code)
    {
        var sb = new StringBuilder(code.Length);
        foreach (var c in code)
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToUpperInvariant(c));
        return sb.ToString();
    }

    private static string Group(string text)
    {
        var sb = new StringBuilder(text.Length + text.Length / 5);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(text[i]);
        }
        return sb.ToString();
    }
}
