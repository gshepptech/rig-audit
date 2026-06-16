using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using RigAudit.Collectors;
using RigAudit.Core.Comparison;
using RigAudit.Core.Findings;
using RigAudit.Core.Models;
using RigAudit.Export;

namespace RigAudit.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private const string NvidiaDriverUrl = "https://www.nvidia.com/Download/index.aspx";
    private const string AmdDriverUrl = "https://www.amd.com/en/support/download/drivers.html";
    private const string IntelDriverUrl = "https://www.intel.com/content/www/us/en/download-center/home.html";

    private ViewState _currentView = ViewState.Home;
    private ScanStatus _scanStatus = ScanStatus.Idle;
    private string _statusText = "Status: idle";
    private string? _outputDir;
    private RigSnapshot? _snapshot;
    private bool _redactPersonalIdentifiers = true;
    private static readonly string[] ExpectedRuleTitles =
    [
        "Configured memory speed is below the expected baseline.",
        "Power plan is set to Balanced.",
        "CPU package temperature is above the configured threshold.",
        "Some data could not be detected; running as admin may improve detection."
    ];

    public MainViewModel()
    {
        RunScanCommand = new AsyncRelayCommand(RunScanAsync, () => ScanStatus != ScanStatus.Scanning, HandleCommandException);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
        ExportJsonCommand = new AsyncRelayCommand(ExportJsonAsync, onException: HandleCommandException);
        OpenOfficialDriverPageCommand = new AsyncRelayCommand(OpenOfficialDriverPageAsync, () => HasOfficialDriverLink, HandleCommandException);
        CompareSnapshotsCommand = new AsyncRelayCommand(CompareSnapshotsAsync, onException: HandleCommandException);
        ExportCompareReportCommand = new AsyncRelayCommand(ExportCompareReportAsync, () => HasCompareChanges, HandleCommandException);
        BackToHomeCommand = new RelayCommand(() => CurrentView = ViewState.Home);
    }

    public ViewState CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public ScanStatus ScanStatus
    {
        get => _scanStatus;
        set
        {
            SetProperty(ref _scanStatus, value);
            StatusText = value switch
            {
                ScanStatus.Idle => "Status: idle",
                ScanStatus.Scanning => "Status: scanning",
                ScanStatus.Done => "Status: done",
                ScanStatus.Error => "Status: error",
                _ => "Unknown"
            };
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public bool RedactPersonalIdentifiers
    {
        get => _redactPersonalIdentifiers;
        set => SetProperty(ref _redactPersonalIdentifiers, value);
    }

    public ObservableCollection<Finding> Findings { get; } = [];
    public ObservableCollection<CollectorRunInfo> CollectorDetails { get; } = [];
    public ObservableCollection<StorageDriveInfo> StorageDrives { get; } = [];
    public ObservableCollection<DeviceInfo> DeviceInventory { get; } = [];
    public ObservableCollection<string> CompareChanges { get; } = [];

    // Display properties — "Unknown" fallback for null values
    public string DisplayComputerName => _snapshot?.Machine.ComputerName ?? "Unknown";
    public string DisplayWindowsUser => _snapshot?.Machine.WindowsUser ?? "Unknown";
    public string DisplayTimestampUtc => _snapshot?.TimestampUtc.ToString("u") ?? "Unknown";
    public string DisplayWindowsEdition => _snapshot?.OS.WindowsEdition ?? "Unknown";
    public string DisplayWindowsVersion => _snapshot?.OS.WindowsVersion ?? "Unknown";
    public string DisplayWindowsBuild => _snapshot?.OS.WindowsBuildNumber ?? "Unknown";
    public string DisplayCpuName => _snapshot?.CPU.Name ?? "Unknown";
    public string DisplayCpuCores => _snapshot?.CPU.PhysicalCores is { } pc
        ? $"{pc} physical / {_snapshot?.CPU.LogicalCores?.ToString() ?? "?"} logical"
        : "Unknown";
    public string DisplayGpuName => _snapshot?.GPU.Name ?? "Unknown";
    public string DisplayGpuVendor => _snapshot?.GPU.Vendor.ToString() ?? "Unknown";
    public string DisplayGpuDriver => _snapshot?.GPU.DriverVersion ?? "Unknown";
    public string DisplayTotalMemory => _snapshot?.Memory.TotalPhysicalGb is { } gb
        ? $"{gb:F1} GB"
        : "Unknown";
    public string DisplayMemorySpeed => _snapshot?.Memory.ConfiguredClockMhz is { } mhz
        ? $"{mhz} MHz"
        : "Unknown";
    public string DisplayPowerPlan => _snapshot?.Power.ActivePowerPlanName ?? "Unknown";
    public string DisplayPowerPlanGuid => _snapshot?.Power.ActivePowerPlanGuid ?? "Unknown";
    public string DisplayCpuTemp => _snapshot?.Sensors.CpuPackageTempC is { } ct
        ? $"{ct:F1}\u00b0C"
        : "Unknown";
    public string DisplayGpuTemp => _snapshot?.Sensors.GpuTempC is { } gt
        ? $"{gt:F1}\u00b0C"
        : "Unknown";
    public string DisplayCpuLoad => _snapshot?.Sensors.CpuLoadPercent is { } cl
        ? $"{cl:F1}%"
        : "Unknown";
    public string DisplayGpuLoad => _snapshot?.Sensors.GpuLoadPercent is { } gl
        ? $"{gl:F1}%"
        : "Unknown";
    public bool HasOfficialDriverLink => GetOfficialDriverUrl() is not null;
    public string DriverSupportButtonText => _snapshot?.GPU.Vendor switch
    {
        GpuVendor.Nvidia => "Open NVIDIA Driver Page",
        GpuVendor.Amd => "Open AMD Driver Page",
        GpuVendor.Intel => "Open Intel Driver Page",
        _ => "Driver Link Unavailable"
    };
    public string DriverSupportHintText => HasOfficialDriverLink
        ? "Opens the official vendor driver page in your default browser."
        : "Official driver link is unavailable for unknown GPU vendor.";
    public bool HasCompareChanges => CompareChanges.Count > 0;

    public ICommand RunScanCommand { get; }
    public ICommand OpenOutputFolderCommand { get; }
    public ICommand ExportJsonCommand { get; }
    public ICommand OpenOfficialDriverPageCommand { get; }
    public ICommand CompareSnapshotsCommand { get; }
    public ICommand ExportCompareReportCommand { get; }
    public ICommand BackToHomeCommand { get; }

    private async Task RunScanAsync()
    {
        ScanStatus = ScanStatus.Scanning;
        StatusText = "Scanning...";
        Findings.Clear();
        CollectorDetails.Clear();
        var dispatcher = Application.Current?.Dispatcher;
        var runSw = Stopwatch.StartNew();

        try
        {
            var completedScan = await Task.Run(async () =>
            {
                var runner = new ScanRunner();
                var result = runner.Run();
                var outputDir = OutputPaths.CreateOutputDirectory();
                await SnapshotExporter.WriteSnapshotAsync(result.Snapshot, outputDir, redactPersonalIdentifiers: false);
                await SnapshotExporter.WriteLogAsync(result.Log.ToString(), outputDir);
                if (result.Log.HasDebugDetails)
                    await SnapshotExporter.WriteDebugLogAsync(result.Log.DebugToString(), outputDir);
                return new CompletedScan(result, outputDir);
            });

            if (dispatcher is null)
                throw new InvalidOperationException("WPF dispatcher is unavailable.");

            await dispatcher.InvokeAsync(() =>
            {
                _snapshot = completedScan.Result.Snapshot;
                _outputDir = completedScan.OutputDirectory;

                foreach (var finding in BuildDisplayFindings(completedScan.Result.Findings))
                    Findings.Add(finding);
                foreach (var collectorRun in completedScan.Result.CollectorRuns)
                    CollectorDetails.Add(collectorRun);
                SyncStorageAndDeviceCollections(completedScan.Result.Snapshot);

                ScanStatus = ScanStatus.Done;
                CurrentView = ViewState.Results;
                runSw.Stop();
                StatusText = $"Scan complete in {runSw.Elapsed.TotalSeconds:F2}s. Output saved to: {_outputDir}";
                NotifyAllDisplayProperties();
                NotifyDriverProperties();
            }).Task;
        }
        catch (Exception)
        {
            if (dispatcher is null)
            {
                ScanStatus = ScanStatus.Error;
                StatusText = "Scan could not be completed. Review scan.log for details.";
                return;
            }

            await dispatcher.InvokeAsync(() =>
            {
                runSw.Stop();
                ScanStatus = ScanStatus.Error;
                StatusText = "Scan could not be completed. Review scan.log for details.";
            }).Task;
        }
    }

    private void OpenOutputFolder()
    {
        if (string.IsNullOrWhiteSpace(_outputDir) || !Directory.Exists(_outputDir))
        {
            StatusText = "Run a scan first.";
            return;
        }

        try
        {
            var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(_outputDir, requireExisting: true);
            var explorerPath = Path.Combine(Environment.SystemDirectory, "explorer.exe");
            if (!File.Exists(explorerPath))
                throw new FileNotFoundException("explorer.exe was not found in System32.", explorerPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = explorerPath,
                UseShellExecute = false,
                WorkingDirectory = Environment.SystemDirectory
            };
            startInfo.ArgumentList.Add(safeOutputDir);

            Process.Start(startInfo);
            _ = AppendActionLogAsync("action=open_output_folder status=ok");
        }
        catch (Exception)
        {
            StatusText = "Failed to open output folder.";
            _ = AppendActionLogAsync("action=open_output_folder status=error");
        }
    }

    private async Task ExportJsonAsync()
    {
        if (_snapshot is null || string.IsNullOrWhiteSpace(_outputDir))
        {
            StatusText = "No scan results to export.";
            return;
        }

        try
        {
            var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(_outputDir, requireExisting: true);
            var exportPath = await SnapshotExporter.WriteExportSnapshotAsync(_snapshot, safeOutputDir, RedactPersonalIdentifiers);
            StatusText = $"Exported RigSnapshot.export.json to: {exportPath}";
            await AppendActionLogAsync($"action=export_snapshot status=ok redacted={RedactPersonalIdentifiers.ToString().ToLowerInvariant()}");
        }
        catch (Exception)
        {
            StatusText = "Export failed.";
            await AppendActionLogAsync($"action=export_snapshot status=error redacted={RedactPersonalIdentifiers.ToString().ToLowerInvariant()}");
        }
    }

    private async Task OpenOfficialDriverPageAsync()
    {
        var url = GetOfficialDriverUrl();
        if (url is null)
        {
            StatusText = "Official driver link unavailable for unknown GPU vendor.";
            return;
        }

        try
        {
            var explorerPath = Path.Combine(Environment.SystemDirectory, "explorer.exe");
            if (!File.Exists(explorerPath))
                throw new FileNotFoundException("explorer.exe was not found in System32.", explorerPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = explorerPath,
                UseShellExecute = false,
                WorkingDirectory = Environment.SystemDirectory
            };
            startInfo.ArgumentList.Add(url);
            Process.Start(startInfo);

            StatusText = "Opened official driver page in browser.";
            await AppendActionLogAsync($"action=open_driver_page status=ok vendor={_snapshot?.GPU.Vendor}");
        }
        catch (Exception)
        {
            StatusText = "Failed to open official driver page.";
            await AppendActionLogAsync($"action=open_driver_page status=error vendor={_snapshot?.GPU.Vendor}");
        }
    }

    private async Task CompareSnapshotsAsync()
    {
        var firstPath = SelectSnapshotPath("Select first snapshot");
        if (firstPath is null)
        {
            StatusText = "Snapshot compare canceled.";
            return;
        }

        var secondPath = SelectSnapshotPath("Select second snapshot");
        if (secondPath is null)
        {
            StatusText = "Snapshot compare canceled.";
            return;
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            options.Converters.Add(new JsonStringEnumConverter());

            var first = JsonSerializer.Deserialize<RigSnapshot>(await File.ReadAllTextAsync(firstPath), options);
            var second = JsonSerializer.Deserialize<RigSnapshot>(await File.ReadAllTextAsync(secondPath), options);
            if (first is null || second is null)
            {
                StatusText = "Compare failed: unable to parse one or both snapshots.";
                return;
            }

            var compareResult = SnapshotComparer.Compare(first, second);
            CompareChanges.Clear();
            foreach (var change in compareResult.Changes)
                CompareChanges.Add(change);

            OnPropertyChanged(nameof(HasCompareChanges));
            CommandManager.InvalidateRequerySuggested();
            StatusText = $"Compare complete: {compareResult.Changes.Count} item(s).";
            await AppendActionLogAsync($"action=compare_snapshots status=ok changes={compareResult.Changes.Count}");
        }
        catch (Exception)
        {
            StatusText = "Compare failed.";
            await AppendActionLogAsync("action=compare_snapshots status=error");
        }
    }

    private async Task ExportCompareReportAsync()
    {
        if (CompareChanges.Count == 0)
        {
            StatusText = "No compare results to export.";
            return;
        }

        try
        {
            _outputDir ??= OutputPaths.CreateOutputDirectory();
            var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(_outputDir, requireExisting: true);
            var report = BuildCompareReportText();
            var reportPath = await SnapshotExporter.WriteCompareReportAsync(report, safeOutputDir);
            StatusText = $"Exported compare report to: {reportPath}";
            await AppendActionLogAsync("action=export_compare_report status=ok");
        }
        catch (Exception)
        {
            StatusText = "Compare report export failed.";
            await AppendActionLogAsync("action=export_compare_report status=error");
        }
    }

    private void HandleCommandException(Exception _)
    {
        ScanStatus = ScanStatus.Error;
        StatusText = "The requested operation could not be completed.";
    }

    private void NotifyAllDisplayProperties()
    {
        OnPropertyChanged(nameof(DisplayComputerName));
        OnPropertyChanged(nameof(DisplayWindowsUser));
        OnPropertyChanged(nameof(DisplayTimestampUtc));
        OnPropertyChanged(nameof(DisplayWindowsEdition));
        OnPropertyChanged(nameof(DisplayWindowsVersion));
        OnPropertyChanged(nameof(DisplayWindowsBuild));
        OnPropertyChanged(nameof(DisplayCpuName));
        OnPropertyChanged(nameof(DisplayCpuCores));
        OnPropertyChanged(nameof(DisplayGpuName));
        OnPropertyChanged(nameof(DisplayGpuVendor));
        OnPropertyChanged(nameof(DisplayGpuDriver));
        OnPropertyChanged(nameof(DisplayTotalMemory));
        OnPropertyChanged(nameof(DisplayMemorySpeed));
        OnPropertyChanged(nameof(DisplayPowerPlan));
        OnPropertyChanged(nameof(DisplayPowerPlanGuid));
        OnPropertyChanged(nameof(DisplayCpuTemp));
        OnPropertyChanged(nameof(DisplayGpuTemp));
        OnPropertyChanged(nameof(DisplayCpuLoad));
        OnPropertyChanged(nameof(DisplayGpuLoad));
    }

    private void NotifyDriverProperties()
    {
        OnPropertyChanged(nameof(HasOfficialDriverLink));
        OnPropertyChanged(nameof(DriverSupportButtonText));
        OnPropertyChanged(nameof(DriverSupportHintText));
        CommandManager.InvalidateRequerySuggested();
    }

    private static IEnumerable<Finding> BuildDisplayFindings(IReadOnlyCollection<Finding> evaluatedFindings)
    {
        var emittedTitles = new HashSet<string>(StringComparer.Ordinal);

        foreach (var expectedTitle in ExpectedRuleTitles)
        {
            var existing = evaluatedFindings.FirstOrDefault(f =>
                string.Equals(f.Title, expectedTitle, StringComparison.Ordinal));
            if (existing is not null)
            {
                emittedTitles.Add(existing.Title);
                yield return existing;
                continue;
            }

            yield return new Finding
            {
                Severity = Severity.Info,
                Title = expectedTitle,
                Summary = "No issues detected by this rule in the current scan.",
                SuggestedAction = "No action required."
            };
            emittedTitles.Add(expectedTitle);
        }

        foreach (var extra in evaluatedFindings.Where(f => !emittedTitles.Contains(f.Title)))
        {
            yield return extra;
            emittedTitles.Add(extra.Title);
        }
    }

    private string? GetOfficialDriverUrl()
    {
        var vendor = _snapshot?.GPU.Vendor ?? GpuVendor.Unknown;
        return vendor switch
        {
            GpuVendor.Nvidia => NvidiaDriverUrl,
            GpuVendor.Amd => AmdDriverUrl,
            GpuVendor.Intel => IntelDriverUrl,
            _ => null
        };
    }

    private async Task AppendActionLogAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(_outputDir))
            return;

        try
        {
            var safeOutputDir = OutputPaths.EnsureOutputDirectoryIsConstrained(_outputDir, requireExisting: true);
            var logPath = OutputPaths.LogPath(safeOutputDir);
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [INFO] {message}{Environment.NewLine}";
            await File.AppendAllTextAsync(logPath, line);
        }
        catch
        {
            // Intentionally ignored. Action logging must not break UI actions.
        }
    }

    private static string? SelectSnapshotPath(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Rig Snapshot JSON|RigSnapshot*.json|JSON files|*.json|All files|*.*",
            CheckFileExists = true
        };

        if (Directory.Exists(OutputPaths.Root))
            dialog.InitialDirectory = OutputPaths.Root;

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private string BuildCompareReportText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Rig Audit Pro Snapshot Compare");
        sb.AppendLine($"GeneratedUtc: {DateTime.UtcNow:O}");
        sb.AppendLine();
        foreach (var line in CompareChanges)
            sb.AppendLine($"- {line}");
        return sb.ToString();
    }

    private void SyncStorageAndDeviceCollections(RigSnapshot snapshot)
    {
        StorageDrives.Clear();
        foreach (var drive in snapshot.Storage.Drives)
            StorageDrives.Add(drive);

        DeviceInventory.Clear();
        foreach (var device in snapshot.Devices)
            DeviceInventory.Add(device);
    }

    private sealed record CompletedScan(ScanResult Result, string OutputDirectory);
}
