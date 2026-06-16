using System.Diagnostics;
using System.Linq;
using RigAudit.Collectors.Collectors;
using RigAudit.Collectors.Logging;
using RigAudit.Core.Findings;
using RigAudit.Core.Models;
using RigAudit.Core.Rules;

namespace RigAudit.Collectors;

public class ScanResult
{
    public required RigSnapshot Snapshot { get; init; }
    public required List<Finding> Findings { get; init; }
    public required ScanLog Log { get; init; }
    public required List<CollectorRunInfo> CollectorRuns { get; init; }
    public required long TotalDurationMs { get; init; }
}

public class CollectorRunInfo
{
    public required string CollectorName { get; init; }
    public required string Status { get; init; }
    public required long DurationMs { get; init; }
}

public class ScanRunner
{
    private readonly int _collectorTimeoutMs;
    private readonly bool _debugMode;

    private static readonly ICollector[] Collectors =
    [
        new OsCollector(),
        new CpuCollector(),
        new GpuCollector(),
        new MemoryCollector(),
        new PowerPlanCollector(),
        new SensorCollector(),
        new StorageCollector(),
        new DeviceInventoryCollector()
    ];

    public ScanRunner(int collectorTimeoutMs = 2500, bool? debugMode = null)
    {
        _collectorTimeoutMs = collectorTimeoutMs <= 0 ? 2500 : collectorTimeoutMs;
        _debugMode = debugMode ?? IsDebugBuild();
    }

    public ScanResult Run()
    {
        var snapshot = new RigSnapshot();
        var log = new ScanLog(_debugMode);
        var collectorRuns = new List<CollectorRunInfo>();
        var totalSw = Stopwatch.StartNew();

        log.Info("Scan started.");

        foreach (var collector in Collectors)
        {
            var startedUtc = DateTime.UtcNow;
            var sw = Stopwatch.StartNew();
            log.Info($"collector={collector.Name} phase=start startedUtc={startedUtc:O}");

            var workingSnapshot = CloneSnapshot(snapshot);
            var collectTask = Task.Run(() => collector.Collect(workingSnapshot));
            var timeoutTask = Task.Delay(_collectorTimeoutMs);
            var completedTask = Task.WhenAny(collectTask, timeoutTask).GetAwaiter().GetResult();

            if (completedTask != collectTask)
            {
                sw.Stop();
                var endedUtc = DateTime.UtcNow;
                log.Fail(
                    $"collector={collector.Name} phase=end status=timeout startedUtc={startedUtc:O} endedUtc={endedUtc:O} durationMs={sw.ElapsedMilliseconds} timeoutMs={_collectorTimeoutMs}");
                log.Debug(
                    $"collector={collector.Name} timed out; collection continues on a detached snapshot instance.");
                collectorRuns.Add(new CollectorRunInfo
                {
                    CollectorName = collector.Name,
                    Status = "timeout",
                    DurationMs = sw.ElapsedMilliseconds
                });
                continue;
            }

            sw.Stop();
            var endUtc = DateTime.UtcNow;
            if (collectTask.IsFaulted)
            {
                var exception = collectTask.Exception?.GetBaseException() ?? new Exception("Unknown collector failure.");
                var sanitized = BuildSanitizedException(exception);
                log.Fail(
                    $"collector={collector.Name} phase=end status=error startedUtc={startedUtc:O} endedUtc={endUtc:O} durationMs={sw.ElapsedMilliseconds} {sanitized}");
                log.Debug($"collector={collector.Name} fullException={exception}");
                collectorRuns.Add(new CollectorRunInfo
                {
                    CollectorName = collector.Name,
                    Status = "error",
                    DurationMs = sw.ElapsedMilliseconds
                });
                continue;
            }

            if (collectTask.IsCanceled)
            {
                log.Fail(
                    $"collector={collector.Name} phase=end status=canceled startedUtc={startedUtc:O} endedUtc={endUtc:O} durationMs={sw.ElapsedMilliseconds}");
                log.Debug($"collector={collector.Name} collection task was canceled.");
                collectorRuns.Add(new CollectorRunInfo
                {
                    CollectorName = collector.Name,
                    Status = "canceled",
                    DurationMs = sw.ElapsedMilliseconds
                });
                continue;
            }

            snapshot = workingSnapshot;
            log.Ok(
                $"collector={collector.Name} phase=end status=ok startedUtc={startedUtc:O} endedUtc={endUtc:O} durationMs={sw.ElapsedMilliseconds}");
            collectorRuns.Add(new CollectorRunInfo
            {
                CollectorName = collector.Name,
                Status = "ok",
                DurationMs = sw.ElapsedMilliseconds
            });
        }

        var findings = RuleEngine.Evaluate(snapshot);
        foreach (var finding in findings)
        {
            log.Info($"Finding [{finding.Severity}]: {finding.Title}");
        }

        totalSw.Stop();
        log.Info($"Scan completed in {totalSw.ElapsedMilliseconds}ms with {findings.Count} finding(s).");

        return new ScanResult
        {
            Snapshot = snapshot,
            Findings = findings,
            Log = log,
            CollectorRuns = collectorRuns,
            TotalDurationMs = totalSw.ElapsedMilliseconds
        };
    }

    private static bool IsDebugBuild()
    {
#if DEBUG
        return true;
#else
        return false;
#endif
    }

    private static string BuildSanitizedException(Exception ex)
    {
        var code = $"0x{ex.HResult:X8}";
        return $"exceptionType={ex.GetType().Name} errorCode={code}";
    }

    private static RigSnapshot CloneSnapshot(RigSnapshot source)
    {
        return new RigSnapshot
        {
            TimestampUtc = source.TimestampUtc,
            Machine = new MachineInfo
            {
                ComputerName = source.Machine.ComputerName,
                WindowsUser = source.Machine.WindowsUser
            },
            OS = new OsInfo
            {
                WindowsEdition = source.OS.WindowsEdition,
                WindowsVersion = source.OS.WindowsVersion,
                WindowsBuildNumber = source.OS.WindowsBuildNumber
            },
            CPU = new CpuInfo
            {
                Name = source.CPU.Name,
                PhysicalCores = source.CPU.PhysicalCores,
                LogicalCores = source.CPU.LogicalCores
            },
            GPU = new GpuInfo
            {
                Name = source.GPU.Name,
                Vendor = source.GPU.Vendor,
                DriverVersion = source.GPU.DriverVersion
            },
            Memory = new MemoryInfo
            {
                TotalPhysicalGb = source.Memory.TotalPhysicalGb,
                ConfiguredClockMhz = source.Memory.ConfiguredClockMhz
            },
            Power = new PowerInfo
            {
                ActivePowerPlanName = source.Power.ActivePowerPlanName,
                ActivePowerPlanGuid = source.Power.ActivePowerPlanGuid
            },
            Sensors = new SensorInfo
            {
                CpuPackageTempC = source.Sensors.CpuPackageTempC,
                GpuTempC = source.Sensors.GpuTempC,
                CpuLoadPercent = source.Sensors.CpuLoadPercent,
                GpuLoadPercent = source.Sensors.GpuLoadPercent
            },
            Storage = new StorageInfo
            {
                Drives = source.Storage.Drives.Select(d => new StorageDriveInfo
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
            Devices = source.Devices.Select(d => new DeviceInfo
            {
                Category = d.Category,
                FriendlyName = d.FriendlyName,
                DriverProvider = d.DriverProvider,
                DriverVersion = d.DriverVersion,
                DriverDate = d.DriverDate
            }).ToList()
        };
    }
}
