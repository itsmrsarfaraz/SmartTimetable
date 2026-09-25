using System.Text;

namespace SmartTimetable.Licensing;

/// <summary>Base32 without ambiguous characters (no I, O, 0, 1) so codes can be read over the phone.</summary>
internal static class Base32
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Encode(ReadOnlySpan<byte> data)
    {
        var sb = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                sb.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) sb.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static bool TryDecode(string text, out byte[] data)
    {
        var output = new List<byte>(text.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;
        foreach (var c in text)
        {
            var index = Alphabet.IndexOf(c);
            if (index < 0) { data = []; return false; }
            buffer = (buffer << 5) | index;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
                buffer &= (1 << bits) - 1;
            }
        }
        data = output.ToArray();
        return true;
    }
}

/// <summary>CRC-16/CCITT-FALSE - catches typos in request codes.</summary>
internal static class Crc16
{
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc;
    }
}
