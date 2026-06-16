using System.Management;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class CpuCollector : ICollector
{
    public string Name => "CPU";

    public void Collect(RigSnapshot snapshot)
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
        foreach (var obj in searcher.Get())
        {
            snapshot.CPU.Name = obj["Name"]?.ToString()?.Trim();

            if (obj["NumberOfCores"] is uint cores)
                snapshot.CPU.PhysicalCores = (int)cores;

            if (obj["NumberOfLogicalProcessors"] is uint logical)
                snapshot.CPU.LogicalCores = (int)logical;

            break; // Use first processor
        }
    }
}
