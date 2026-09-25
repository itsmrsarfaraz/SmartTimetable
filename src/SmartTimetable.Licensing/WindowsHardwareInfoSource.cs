using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SmartTimetable.Licensing;

/// <summary>Reads hardware identifiers on Windows via the registry and WMI. Never throws.</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsHardwareInfoSource : IHardwareInfoSource
{
    public string? Read(HardwareSlot slot) => slot switch
    {
        HardwareSlot.MachineGuid => ReadMachineGuid(),
        HardwareSlot.SystemUuid => QueryFirst("Win32_ComputerSystemProduct", "UUID"),
        HardwareSlot.BiosSerial => QueryFirst("Win32_BIOS", "SerialNumber"),
        HardwareSlot.BoardSerial => QueryFirst("Win32_BaseBoard", "SerialNumber"),
        HardwareSlot.DiskSerial => ReadDiskSerial(),
        _ => null,
    };

    private static string? ReadMachineGuid()
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
            return key?.GetValue("MachineGuid") as string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private static string? QueryFirst(string wmiClass, string property)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
            foreach (var item in searcher.Get())
            {
                using (item)
                {
                    var value = item[property]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value)) return value;
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            // WMI unavailable or blocked - slot simply stays empty.
        }
        return null;
    }

    private static string? ReadDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Index, InterfaceType, SerialNumber FROM Win32_DiskDrive");

            string? best = null;
            var bestIndex = uint.MaxValue;
            foreach (var item in searcher.Get())
            {
                using (item)
                {
                    var interfaceType = item["InterfaceType"]?.ToString();
                    var serial = item["SerialNumber"]?.ToString()?.Trim();
                    if (string.Equals(interfaceType, "USB", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrWhiteSpace(serial)) continue;

                    var index = item["Index"] is uint i ? i : uint.MaxValue - 1;
                    if (index < bestIndex) { bestIndex = index; best = serial; }
                }
            }
            return best;
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
