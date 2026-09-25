using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace SmartTimetable.Licensing;

/// <summary>
/// A privacy-preserving fingerprint of one computer: up to <see cref="SlotCount"/> truncated HMAC
/// hashes of hardware identifiers. Slots that could not be read (or held junk) are null.
/// </summary>
public sealed class MachineFingerprint
{
    public const int SlotCount = 5;
    public const int HashLength = 6;

    private const int ValidMaskBits = (1 << SlotCount) - 1;
    private static readonly byte[] HmacKey = Encoding.UTF8.GetBytes("SmartTimetable.AI/machine-fingerprint/v1");

    private readonly byte[]?[] _slots = new byte[]?[SlotCount];

    public MachineFingerprint(IReadOnlyList<byte[]?> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);
        if (slots.Count != SlotCount)
            throw new ArgumentException($"Exactly {SlotCount} slots are required.", nameof(slots));

        for (var i = 0; i < SlotCount; i++)
        {
            var slot = slots[i];
            if (slot is not null && slot.Length != HashLength)
                throw new ArgumentException($"Slot {i} must be {HashLength} bytes.", nameof(slots));
            _slots[i] = slot is null ? null : (byte[])slot.Clone();
        }
    }

    /// <summary>Bit i is set when slot i is present.</summary>
    public byte PresentMask
    {
        get
        {
            var mask = 0;
            for (var i = 0; i < SlotCount; i++)
                if (_slots[i] is not null) mask |= 1 << i;
            return (byte)mask;
        }
    }

    public int PresentCount => BitOperations.PopCount(PresentMask);

    /// <summary>Number of slots that are present in BOTH fingerprints and equal.</summary>
    public int CountMatchesWith(MachineFingerprint other)
    {
        ArgumentNullException.ThrowIfNull(other);
        var matches = 0;
        for (var i = 0; i < SlotCount; i++)
        {
            var a = _slots[i];
            var b = other._slots[i];
            if (a is null || b is null) continue;
            if (CryptographicOperations.FixedTimeEquals(a, b)) matches++;
        }
        return matches;
    }

    /// <summary>Compact form: [mask][hash of each present slot, in slot order].</summary>
    public byte[] ToBytes()
    {
        var bytes = new List<byte>(1 + PresentCount * HashLength) { PresentMask };
        foreach (var slot in _slots)
            if (slot is not null) bytes.AddRange(slot);
        return bytes.ToArray();
    }

    public static bool TryFromBytes(ReadOnlySpan<byte> data, [NotNullWhen(true)] out MachineFingerprint? fingerprint)
    {
        fingerprint = null;
        if (data.Length < 1) return false;

        var mask = data[0];
        if ((mask & ~ValidMaskBits) != 0) return false;
        if (data.Length != 1 + BitOperations.PopCount(mask) * HashLength) return false;

        var slots = new byte[]?[SlotCount];
        var offset = 1;
        for (var i = 0; i < SlotCount; i++)
        {
            if ((mask & (1 << i)) == 0) continue;
            slots[i] = data.Slice(offset, HashLength).ToArray();
            offset += HashLength;
        }

        fingerprint = new MachineFingerprint(slots);
        return true;
    }

    /// <summary>Hashes raw hardware strings into a fingerprint. Junk/empty values become absent slots.</summary>
    public static MachineFingerprint FromHardware(IReadOnlyDictionary<HardwareSlot, string?> raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var slots = new byte[]?[SlotCount];
        for (var i = 0; i < SlotCount; i++)
        {
            var slot = (HardwareSlot)i;
            raw.TryGetValue(slot, out var value);
            var normalized = HardwareValue.Normalize(value);
            if (normalized is null) continue;

            var mac = HMACSHA256.HashData(HmacKey, Encoding.UTF8.GetBytes($"{slot}|{normalized}"));
            slots[i] = mac.AsSpan(0, HashLength).ToArray();
        }
        return new MachineFingerprint(slots);
    }
}
