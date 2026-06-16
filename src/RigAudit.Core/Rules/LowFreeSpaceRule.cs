using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public class LowFreeSpaceRule : IRule
{
    private const double ThresholdPercent = 15.0;

    public Finding? Evaluate(RigSnapshot snapshot)
    {
        var lowSpaceVolumes = snapshot.Storage.Drives
            .Where(d => d.FreePercent is not null && d.FreePercent < ThresholdPercent)
            .Select(d => d.VolumeName ?? "Unknown")
            .ToList();

        if (lowSpaceVolumes.Count == 0)
            return null;

        return new Finding
        {
            Severity = Severity.Warning,
            Title = "Low free space can impact performance and updates.",
            Summary = $"Volumes below {ThresholdPercent}% free space: {string.Join(", ", lowSpaceVolumes)}.",
            SuggestedAction = "Free disk space by moving or removing unneeded files."
        };
    }
}
