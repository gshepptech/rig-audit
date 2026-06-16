using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public class BalancedPowerPlanRule : IRule
{
    public Finding? Evaluate(RigSnapshot snapshot)
    {
        var planName = snapshot.Power.ActivePowerPlanName;
        if (string.IsNullOrEmpty(planName))
            return null;

        if (!planName.Contains("Balanced", StringComparison.OrdinalIgnoreCase))
            return null;

        return new Finding
        {
            Severity = Severity.Info,
            Title = "Power plan is set to Balanced.",
            Summary = "The Balanced plan can reduce sustained CPU boost behavior in some gaming workloads.",
            SuggestedAction = "If consistent peak performance is preferred, compare results with a High Performance plan."
        };
    }
}
