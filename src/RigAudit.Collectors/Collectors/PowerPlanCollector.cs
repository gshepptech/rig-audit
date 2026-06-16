using System.Diagnostics;
using System.Text.RegularExpressions;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public partial class PowerPlanCollector : ICollector
{
    private const int TimeoutMs = 5000;

    public string Name => "PowerPlan";

    public void Collect(RigSnapshot snapshot)
    {
        var systemDirectory = Path.GetFullPath(Environment.SystemDirectory);
        var powercfgPath = Path.GetFullPath(Path.Combine(systemDirectory, "powercfg.exe"));
        var expectedDirectory = Path.GetFullPath(Path.GetDirectoryName(powercfgPath) ?? string.Empty);
        if (!string.Equals(expectedDirectory, systemDirectory, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("powercfg.exe resolved outside System32.");

        if (!File.Exists(powercfgPath))
            throw new FileNotFoundException("powercfg.exe was not found in System32.", powercfgPath);

        var startInfo = new ProcessStartInfo
        {
            FileName = powercfgPath,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = systemDirectory
        };
        startInfo.ArgumentList.Add("/getactivescheme");

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start powercfg.exe.");
        var outputTask = process.StandardOutput.ReadToEndAsync();

        if (!process.WaitForExit(TimeoutMs))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"powercfg /getactivescheme timed out after {TimeoutMs}ms.");
        }

        var output = outputTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"powercfg.exe exited with code {process.ExitCode}.");

        // Example output: "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced)"
        var match = PowercfgRegex().Match(output);
        if (match.Success)
        {
            snapshot.Power.ActivePowerPlanGuid = match.Groups[1].Value;
            snapshot.Power.ActivePowerPlanName = match.Groups[2].Value;
        }
    }

    [GeneratedRegex(@"GUID:\s*([0-9a-fA-F\-]+)\s+\((.+)\)")]
    private static partial Regex PowercfgRegex();
}
