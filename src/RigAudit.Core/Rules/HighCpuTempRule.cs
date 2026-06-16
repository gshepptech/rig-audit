using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public class HighCpuTempRule : IRule
{
    private const double ThresholdCelsius = 95.0;

    public Finding? Evaluate(RigSnapshot snapshot)
    {
        var temp = snapshot.Sensors.CpuPackageTempC;
        if (temp is null || temp < ThresholdCelsius)
            return null;

        return new Finding
        {
            Severity = Severity.Warning,
            Title = "CPU package temperature is above the configured threshold.",
            Summary = $"CPU package temperature is {temp:F1}\u00b0C, which is at or above {ThresholdCelsius}\u00b0C. " +
                      "This can indicate thermal limiting under load.",
            SuggestedAction = "Check that CPU cooler is properly mounted, thermal paste is fresh, and case airflow is adequate."
        };
    }
}
