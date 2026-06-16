using RigAudit.Core.Models;

namespace RigAudit.Collectors;

public interface ICollector
{
    string Name { get; }
    void Collect(RigSnapshot snapshot);
}
