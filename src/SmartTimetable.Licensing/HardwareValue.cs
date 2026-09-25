namespace SmartTimetable.Licensing;

/// <summary>Cleans raw hardware strings and rejects the junk values many OEMs ship.</summary>
internal static class HardwareValue
{
    private static readonly HashSet<string> Placeholders = new(StringComparer.Ordinal)
    {
        "NONE", "N/A", "NA", "UNKNOWN", "NOT SPECIFIED", "NOT APPLICABLE", "DEFAULT STRING",
        "TO BE FILLED BY O.E.M.", "TO BE FILLED BY OEM", "SYSTEM SERIAL NUMBER", "SERIAL NUMBER",
        "BASE BOARD SERIAL NUMBER", "TYPE2 - BOARD SERIAL NUMBER", "123456789", "0123456789",
        "1234567890", "SERIAL NUMBER XXXXXXXX", "O.E.M.", "OEM",
    };

    /// <returns>Upper-cased trimmed value, or null when the value is empty/placeholder.</returns>
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var value = raw.Trim().ToUpperInvariant();
        if (Placeholders.Contains(value)) return null;

        var core = value.Replace("-", "").Replace(" ", "");
        if (core.Length < 4) return null;
        if (core.All(c => c == core[0])) return null;   // 0000..., FFFF..., etc.

        return value;
    }
}
