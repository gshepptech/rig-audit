namespace RigAudit.Core.Models;

public class RigSnapshot
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public MachineInfo Machine { get; set; } = new();
    public OsInfo OS { get; set; } = new();
    public CpuInfo CPU { get; set; } = new();
    public GpuInfo GPU { get; set; } = new();
    public MemoryInfo Memory { get; set; } = new();
    public PowerInfo Power { get; set; } = new();
    public SensorInfo Sensors { get; set; } = new();
    public StorageInfo Storage { get; set; } = new();
    public List<DeviceInfo> Devices { get; set; } = [];
}
