namespace RigAudit.Core.Models;

public class StorageDriveInfo
{
    public string? VolumeName { get; set; }
    public bool IsSystemDrive { get; set; }
    public string? DriveKind { get; set; }
    public string? FileSystem { get; set; }
    public double? TotalSizeGb { get; set; }
    public double? FreeSpaceGb { get; set; }
    public double? FreePercent { get; set; }
    public string? SmartHealthStatus { get; set; }
}
