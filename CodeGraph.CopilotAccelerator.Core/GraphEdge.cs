namespace CodeGraph.CopilotAccelerator.Core;

public sealed class GraphEdge
{
    public required string FromId { get; init; }
    public required string ToId { get; init; }
    public required string Type { get; init; }
}