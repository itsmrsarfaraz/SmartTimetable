namespace SmartTimetable.Licensing;

public interface IHardwareInfoSource
{
    /// <summary>Raw identifier for the slot, or null when unavailable.</summary>
    string? Read(HardwareSlot slot);
}

public interface IMachineFingerprintProvider
{
    MachineFingerprint GetFingerprint();
}

/// <summary>Builds (and caches for the process lifetime) the fingerprint from a hardware source.</summary>
public sealed class MachineFingerprintProvider : IMachineFingerprintProvider
{
    private readonly Lazy<MachineFingerprint> _fingerprint;

    public MachineFingerprintProvider(IHardwareInfoSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _fingerprint = new Lazy<MachineFingerprint>(() => Build(source));
    }

    public MachineFingerprint GetFingerprint() => _fingerprint.Value;

    public static MachineFingerprintProvider ForThisMachine()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("SmartTimetable licensing supports Windows only.");
        return new MachineFingerprintProvider(new WindowsHardwareInfoSource());
    }

    private static MachineFingerprint Build(IHardwareInfoSource source)
    {
        var raw = new Dictionary<HardwareSlot, string?>();
        foreach (var slot in Enum.GetValues<HardwareSlot>())
            raw[slot] = source.Read(slot);
        return MachineFingerprint.FromHardware(raw);
    }
}
