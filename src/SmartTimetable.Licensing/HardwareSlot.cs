namespace SmartTimetable.Licensing;

/// <summary>
/// The hardware/OS identifiers that together form a machine fingerprint.
/// Values are used as bit positions and array indexes - do not reorder.
/// </summary>
/// <remarks>
/// CPU ProcessorId is deliberately NOT used: on Windows it is CPUID data that is identical
/// across all CPUs of the same model, so it does not identify a machine.
/// </remarks>
public enum HardwareSlot
{
    MachineGuid = 0,   // Windows install id - changes when Windows is reinstalled
    SystemUuid = 1,    // SMBIOS system UUID - survives reinstall
    BiosSerial = 2,
    BoardSerial = 3,
    DiskSerial = 4,    // first non-USB physical disk
}
