using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public class MissingDataRule : IRule
{
    public Finding? Evaluate(RigSnapshot snapshot)
    {
        var missing = new List<string>();

        if (string.IsNullOrEmpty(snapshot.Machine.ComputerName)) missing.Add("ComputerName");
        if (string.IsNullOrEmpty(snapshot.Machine.WindowsUser)) missing.Add("WindowsUser");
        if (string.IsNullOrEmpty(snapshot.OS.WindowsEdition)) missing.Add("WindowsEdition");
        if (string.IsNullOrEmpty(snapshot.OS.WindowsVersion)) missing.Add("WindowsVersion");
        if (string.IsNullOrEmpty(snapshot.OS.WindowsBuildNumber)) missing.Add("WindowsBuildNumber");
        if (string.IsNullOrEmpty(snapshot.CPU.Name)) missing.Add("CPU Name");
        if (string.IsNullOrEmpty(snapshot.GPU.Name)) missing.Add("GPU Name");
        if (string.IsNullOrEmpty(snapshot.GPU.DriverVersion)) missing.Add("GPU DriverVersion");
        if (string.IsNullOrEmpty(snapshot.Power.ActivePowerPlanName)) missing.Add("ActivePowerPlanName");
        if (string.IsNullOrEmpty(snapshot.Power.ActivePowerPlanGuid)) missing.Add("ActivePowerPlanGuid");

        if (missing.Count == 0)
            return null;

        return new Finding
        {
            Severity = Severity.Info,
            Title = "Some data could not be detected; running as admin may improve detection.",
            Summary = $"The following fields were not detected: {string.Join(", ", missing)}.",
            SuggestedAction = "If additional fields are needed, optionally run the application as Administrator and scan again."
        };
    }
}
