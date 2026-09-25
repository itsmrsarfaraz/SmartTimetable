using System.Buffers.Text;
using System.Text;

namespace SmartTimetable.Licensing;

/// <summary>
/// Wire format of a license: <c>STT1.&lt;base64url payload&gt;.&lt;base64url signature&gt;</c>.
/// Also parses the armored multi-line form written to .lic files.
/// </summary>
public static class LicenseToken
{
    public const string Prefix = "STT1";

    public static string Format(ReadOnlySpan<byte> payload, ReadOnlySpan<byte> signature) =>
        $"{Prefix}.{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(signature)}";

    /// <summary>Removes armor lines, line breaks and whitespace. Returns null if nothing is left.</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var sb = new StringBuilder(text.Length);
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("-----", StringComparison.Ordinal)) continue;
            foreach (var c in line)
                if (!char.IsWhiteSpace(c)) sb.Append(c);
        }
        return sb.Length == 0 ? null : sb.ToString();
    }

    public static bool TryParse(string? text, out byte[] payload, out byte[] signature)
    {
        payload = [];
        signature = [];

        var token = Normalize(text);
        if (token is null) return false;

        var parts = token.Split('.');
        if (parts.Length != 3 || parts[0] != Prefix) return false;

        try
        {
            payload = Base64Url.DecodeFromChars(parts[1]);
            signature = Base64Url.DecodeFromChars(parts[2]);
            return payload.Length > 0 && signature.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>Wraps the token in armor lines of 64 characters for saving as a .lic file.</summary>
    public static string ToArmored(string token)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-----BEGIN SMARTTIMETABLE LICENSE-----");
        for (var i = 0; i < token.Length; i += 64)
            sb.AppendLine(token.Substring(i, Math.Min(64, token.Length - i)));
        sb.AppendLine("-----END SMARTTIMETABLE LICENSE-----");
        return sb.ToString();
    }
}
