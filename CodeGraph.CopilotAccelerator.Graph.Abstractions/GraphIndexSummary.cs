namespace CodeGraph.CopilotAccelerator.Graph.Abstractions;

public sealed record GraphIndexSummary(
    int Projects,
    int Files,
    int Types,
    int Methods,
    int Calls);
