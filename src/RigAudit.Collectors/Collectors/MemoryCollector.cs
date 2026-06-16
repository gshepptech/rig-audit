using System.Management;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

/// <summary>
/// Collects memory info from WMI.
/// ConfiguredClockMhz uses the max memory Speed across all DIMM modules (MHz).
/// </summary>
public class MemoryCollector : ICollector
{
    public string Name => "Memory";

    public void Collect(RigSnapshot snapshot)
    {
        // Total physical memory from Win32_ComputerSystem
        using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
        {
            foreach (var obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] is ulong bytes)
                    snapshot.Memory.TotalPhysicalGb = Math.Round(bytes / (1024.0 * 1024.0 * 1024.0), 1);
                break;
            }
        }

        // Memory speed from Win32_PhysicalMemory.Speed (max across modules).
        // Falls back to ConfiguredClockSpeed for broader compatibility.
        int maxSpeed = 0;
        using (var searcher = new ManagementObjectSearcher("SELECT Speed, ConfiguredClockSpeed FROM Win32_PhysicalMemory"))
        {
            foreach (var obj in searcher.Get())
            {
                var speed = obj["Speed"] as uint? ?? obj["ConfiguredClockSpeed"] as uint?;
                if (speed is > 0 && speed > maxSpeed)
                    maxSpeed = (int)speed.Value;
            }
        }

        if (maxSpeed > 0)
            snapshot.Memory.ConfiguredClockMhz = maxSpeed;
    }
}
