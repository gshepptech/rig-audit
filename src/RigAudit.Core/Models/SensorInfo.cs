namespace RigAudit.Core.Models;

public class SensorInfo
{
    public double? CpuPackageTempC { get; set; }
    public double? GpuTempC { get; set; }
    public double? CpuLoadPercent { get; set; }
    public double? GpuLoadPercent { get; set; }
}
