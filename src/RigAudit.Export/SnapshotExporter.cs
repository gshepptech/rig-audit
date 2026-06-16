using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using RigAudit.Core.Models;

namespace RigAudit.Export;

public static class SnapshotExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task WriteSnapshotAsync(
        RigSnapshot snapshot,
        string outputDir,
        bool redactPersonalIdentifiers = false)
    {
        var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true);
        var exportSnapshot = redactPersonalIdentifiers ? CreateRedactedSnapshot(snapshot) : snapshot;
        var path = OutputPaths.SnapshotPath(safeOutputDir);
        var json = JsonSerializer.Serialize(exportSnapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<string> WriteExportSnapshotAsync(
        RigSnapshot snapshot,
        string outputDir,
        bool redactPersonalIdentifiers)
    {
        var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true);
        var exportSnapshot = redactPersonalIdentifiers ? CreateRedactedSnapshot(snapshot) : snapshot;
        var path = OutputPaths.ExportSnapshotPath(safeOutputDir);
        var json = JsonSerializer.Serialize(exportSnapshot, JsonOptions);
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    public static async Task WriteLogAsync(string logContent, string outputDir)
    {
        var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true);
        var path = OutputPaths.LogPath(safeOutputDir);
        await File.WriteAllTextAsync(path, logContent);
    }

    public static async Task WriteDebugLogAsync(string debugLogContent, string outputDir)
    {
        var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true);
        var path = OutputPaths.DebugLogPath(safeOutputDir);
        await File.WriteAllTextAsync(path, debugLogContent);
    }

    public static async Task<string> WriteCompareReportAsync(string reportContent, string outputDir)
    {
        var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(outputDir, requireExisting: true);
        var path = OutputPaths.CompareReportPath(safeOutputDir);
        await File.WriteAllTextAsync(path, reportContent);
        return path;
    }

    private static RigSnapshot CreateRedactedSnapshot(RigSnapshot snapshot)
    {
        return new RigSnapshot
        {
            TimestampUtc = snapshot.TimestampUtc,
            Machine = new MachineInfo
            {
                ComputerName = Redact(snapshot.Machine.ComputerName),
                WindowsUser = Redact(snapshot.Machine.WindowsUser)
            },
            OS = new OsInfo
            {
                WindowsEdition = snapshot.OS.WindowsEdition,
                WindowsVersion = snapshot.OS.WindowsVersion,
                WindowsBuildNumber = snapshot.OS.WindowsBuildNumber
            },
            CPU = new CpuInfo
            {
                Name = snapshot.CPU.Name,
                PhysicalCores = snapshot.CPU.PhysicalCores,
                LogicalCores = snapshot.CPU.LogicalCores
            },
            GPU = new GpuInfo
            {
                Name = snapshot.GPU.Name,
                Vendor = snapshot.GPU.Vendor,
                DriverVersion = snapshot.GPU.DriverVersion
            },
            Memory = new MemoryInfo
            {
                TotalPhysicalGb = snapshot.Memory.TotalPhysicalGb,
                ConfiguredClockMhz = snapshot.Memory.ConfiguredClockMhz
            },
            Power = new PowerInfo
            {
                ActivePowerPlanName = snapshot.Power.ActivePowerPlanName,
                ActivePowerPlanGuid = snapshot.Power.ActivePowerPlanGuid
            },
            Sensors = new SensorInfo
            {
                CpuPackageTempC = snapshot.Sensors.CpuPackageTempC,
                GpuTempC = snapshot.Sensors.GpuTempC,
                CpuLoadPercent = snapshot.Sensors.CpuLoadPercent,
                GpuLoadPercent = snapshot.Sensors.GpuLoadPercent
            },
            Storage = new StorageInfo
            {
                Drives = snapshot.Storage.Drives.Select(d => new StorageDriveInfo
                {
                    VolumeName = d.VolumeName,
                    IsSystemDrive = d.IsSystemDrive,
                    DriveKind = d.DriveKind,
                    FileSystem = d.FileSystem,
                    TotalSizeGb = d.TotalSizeGb,
                    FreeSpaceGb = d.FreeSpaceGb,
                    FreePercent = d.FreePercent,
                    SmartHealthStatus = d.SmartHealthStatus
                }).ToList()
            },
            Devices = snapshot.Devices.Select(d => new DeviceInfo
            {
                Category = d.Category,
                FriendlyName = d.FriendlyName,
                DriverProvider = d.DriverProvider,
                DriverVersion = d.DriverVersion,
                DriverDate = d.DriverDate
            }).ToList()
        };
    }

    private static string? Redact(string? value)
        => string.IsNullOrEmpty(value) ? value : "REDACTED";
}
