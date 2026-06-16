using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public static class RuleEngine
{
    private static readonly IRule[] Rules =
    [
        new LowMemorySpeedRule(),
        new BalancedPowerPlanRule(),
        new HighCpuTempRule(),
        new MissingDataRule(),
        new LowFreeSpaceRule()
    ];

    public static List<Finding> Evaluate(RigSnapshot snapshot)
    {
        var findings = new List<Finding>();
        foreach (var rule in Rules)
        {
            var finding = rule.Evaluate(snapshot);
            if (finding is not null)
                findings.Add(finding);
        }
        return findings;
    }
}
