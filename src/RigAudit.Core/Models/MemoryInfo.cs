namespace RigAudit.Core.Models;

/// <summary>
/// ConfiguredClockMhz represents the configured memory speed in MHz (max across all modules).
/// </summary>
public class MemoryInfo
{
    public double? TotalPhysicalGb { get; set; }
    public int? ConfiguredClockMhz { get; set; }
}
