using System.Management;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class GpuCollector : ICollector
{
    public string Name => "GPU";

    public void Collect(RigSnapshot snapshot)
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController");
        foreach (var obj in searcher.Get())
        {
            var name = obj["Name"]?.ToString()?.Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            // Skip Microsoft Basic Display Adapter and similar virtual adapters
            if (name.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Virtual", StringComparison.OrdinalIgnoreCase))
                continue;

            snapshot.GPU.Name = name;
            snapshot.GPU.DriverVersion = obj["DriverVersion"]?.ToString();
            snapshot.GPU.Vendor = DeriveVendor(name);
            break; // Use first real GPU
        }
    }

    private static GpuVendor DeriveVendor(string name)
    {
        if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
            return GpuVendor.Nvidia;
        if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
            return GpuVendor.Amd;
        if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
            return GpuVendor.Intel;
        return GpuVendor.Unknown;
    }
}
