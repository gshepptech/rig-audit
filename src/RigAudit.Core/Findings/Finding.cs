namespace RigAudit.Core.Findings;

public class Finding
{
    public required Severity Severity { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string SuggestedAction { get; init; }
}
