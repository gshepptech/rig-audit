using System.Management;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class StorageCollector : ICollector
{
    public string Name => "Storage";

    public void Collect(RigSnapshot snapshot)
    {
        snapshot.Storage ??= new StorageInfo();
        snapshot.Storage.Drives = [];

        var driveKinds = TryGetDriveKinds();
        var systemRoot = NormalizeDrive(Path.GetPathRoot(Environment.SystemDirectory));

        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var normalizedVolume = NormalizeDrive(drive.Name);
            if (normalizedVolume is null)
                continue;

            snapshot.Storage.Drives.Add(new StorageDriveInfo
            {
                VolumeName = normalizedVolume,
                IsSystemDrive = string.Equals(normalizedVolume, systemRoot, StringComparison.OrdinalIgnoreCase),
                DriveKind = driveKinds.TryGetValue(normalizedVolume, out var kind) ? kind : "Unknown",
                FileSystem = drive.DriveFormat,
                TotalSizeGb = Math.Round(drive.TotalSize / (1024d * 1024d * 1024d), 1),
                FreeSpaceGb = Math.Round(drive.AvailableFreeSpace / (1024d * 1024d * 1024d), 1),
                FreePercent = drive.TotalSize == 0 ? null : Math.Round((drive.AvailableFreeSpace * 100d) / drive.TotalSize, 1),
                SmartHealthStatus = "Not supported"
            });
        }
    }

    private static Dictionary<string, string> TryGetDriveKinds()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var diskSearcher = new ManagementObjectSearcher("SELECT DeviceID, Model, InterfaceType, MediaType FROM Win32_DiskDrive");
            foreach (var disk in diskSearcher.Get())
            {
                var deviceId = disk["DeviceID"]?.ToString();
                if (string.IsNullOrWhiteSpace(deviceId))
                    continue;

                var model = disk["Model"]?.ToString();
                var interfaceType = disk["InterfaceType"]?.ToString();
                var mediaType = disk["MediaType"]?.ToString();
                var driveKind = ClassifyDriveKind(model, interfaceType, mediaType);

                foreach (var volume in FindVolumesForDisk(deviceId))
                    result[volume] = driveKind;
            }
        }
        catch
        {
            // Best effort: return what has been discovered so far.
        }

        return result;
    }

    private static IEnumerable<string> FindVolumesForDisk(string diskDeviceId)
    {
        var escapedDiskId = EscapeWmiValue(diskDeviceId);
        var partitionQuery =
            $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{escapedDiskId}'}} WHERE AssocClass = Win32_DiskDriveToDiskPartition";

        using var partitionSearcher = new ManagementObjectSearcher(partitionQuery);
        foreach (var partition in partitionSearcher.Get())
        {
            var partitionDeviceId = partition["DeviceID"]?.ToString();
            if (string.IsNullOrWhiteSpace(partitionDeviceId))
                continue;

            var escapedPartitionId = EscapeWmiValue(partitionDeviceId);
            var logicalQuery =
                $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{escapedPartitionId}'}} WHERE AssocClass = Win32_LogicalDiskToPartition";

            using var logicalSearcher = new ManagementObjectSearcher(logicalQuery);
            foreach (var logical in logicalSearcher.Get())
            {
                var volume = NormalizeDrive(logical["DeviceID"]?.ToString());
                if (!string.IsNullOrWhiteSpace(volume))
                    yield return volume;
            }
        }
    }

    private static string ClassifyDriveKind(string? model, string? interfaceType, string? mediaType)
    {
        if (Contains(model, "NVME") || Contains(interfaceType, "NVME"))
            return "NVMe";
        if (Contains(mediaType, "SSD") || Contains(model, "SSD"))
            return "SSD";
        if (Contains(mediaType, "HDD") || Contains(mediaType, "Fixed hard disk") || Contains(model, "HDD"))
            return "HDD";
        if (Contains(interfaceType, "SATA"))
            return "SATA";
        if (!string.IsNullOrWhiteSpace(interfaceType))
            return interfaceType.Trim();
        return "Unknown";
    }

    private static bool Contains(string? input, string value)
        => !string.IsNullOrWhiteSpace(input) && input.Contains(value, StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeDrive(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim().TrimEnd('\\');
        return normalized.Length >= 2 ? normalized[..2].ToUpperInvariant() : normalized.ToUpperInvariant();
    }

    private static string EscapeWmiValue(string value)
        => value.Replace("\\", "\\\\");
}
