using CodeGraph.CopilotAccelerator.Core;

namespace CodeGraph.CopilotAccelerator.Graph.Abstractions;

public interface ICodeGraphStore
{
    Task UpsertNodeAsync(GraphNode node, CancellationToken ct = default);
    Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct = default);
    Task<IReadOnlyList<GraphNode>> GetCallersAsync(string methodNodeId, CancellationToken ct = default);
    Task<IReadOnlyList<GraphNode>> GetCalleesAsync(string methodNodeId, CancellationToken ct = default);
    Task<GraphIndexSummary> GetSummaryAsync(CancellationToken ct = default);
}
