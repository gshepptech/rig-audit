using RigAudit.Core.Findings;
using RigAudit.Core.Models;

namespace RigAudit.Core.Rules;

public interface IRule
{
    Finding? Evaluate(RigSnapshot snapshot);
}
