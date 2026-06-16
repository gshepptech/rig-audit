using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public class LowMemorySpeedRule : IRule
{
    public Finding? Evaluate(RigSnapshot snapshot)
    {
        var speed = snapshot.Memory.ConfiguredClockMhz;
        if (speed is null)
            return null;

        // Heuristic: speed >= 4000 → DDR5, else DDR4
        bool isDdr5 = speed >= 4000;
        int threshold = isDdr5 ? 4800 : 2666;
        string ddrGen = isDdr5 ? "DDR5" : "DDR4";

        if (speed >= threshold)
            return null;

        return new Finding
        {
            Severity = Severity.Warning,
            Title = "Configured memory speed is below the expected baseline.",
            Summary = $"Configured memory speed is {speed} MHz. Based on the heuristic, this is treated as {ddrGen} with a baseline of {threshold} MHz.",
            SuggestedAction = "If supported by your system, review BIOS/UEFI memory profile settings (XMP/EXPO)."
        };
    }
}
