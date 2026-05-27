namespace CodeGraph.CopilotAccelerator.Core;

public sealed class GraphNode
{
    public required string Id { get; init; }
    public required string Kind { get; init; }
    public required string Label { get; init; }
    public string? FullName { get; init; }
    public string? FilePath { get; init; }
    public int? LineStart { get; init; }
    public int? LineEnd { get; init; }
    public string? Project { get; init; }
    public string? Language { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}