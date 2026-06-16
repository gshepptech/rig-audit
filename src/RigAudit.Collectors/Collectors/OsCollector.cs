using Microsoft.Win32;
using RigAudit.Core.Models;

namespace RigAudit.Collectors.Collectors;

public class OsCollector : ICollector
{
    public string Name => "OS";

    public void Collect(RigSnapshot snapshot)
    {
        snapshot.Machine.ComputerName = Environment.MachineName;
        snapshot.Machine.WindowsUser = Environment.UserName;

        // Read from registry for accurate Windows version info
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
        if (key is not null)
        {
            snapshot.OS.WindowsEdition = key.GetValue("ProductName")?.ToString();
            snapshot.OS.WindowsBuildNumber = key.GetValue("CurrentBuildNumber")?.ToString();

            // DisplayVersion gives "23H2", "24H2", etc.
            var displayVersion = key.GetValue("DisplayVersion")?.ToString();
            snapshot.OS.WindowsVersion = displayVersion ?? key.GetValue("ReleaseId")?.ToString();
        }
    }
}
