namespace RigAudit.Core.Models;

public class GpuInfo
{
    public string? Name { get; set; }
    public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;
    public string? DriverVersion { get; set; }
}
