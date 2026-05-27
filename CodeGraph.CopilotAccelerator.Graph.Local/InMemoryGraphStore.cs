using System.Collections.Concurrent;
using CodeGraph.CopilotAccelerator.Core;
using CodeGraph.CopilotAccelerator.Graph.Abstractions;

namespace CodeGraph.CopilotAccelerator.Graph.Local;

public sealed class InMemoryGraphStore : ICodeGraphStore
{
    private readonly ConcurrentDictionary<string, GraphNode> _nodes = new();
    private readonly ConcurrentBag<GraphEdge> _edges = new();

    public Task UpsertNodeAsync(GraphNode node, CancellationToken ct = default)
    {
        _nodes[node.Id] = node;
        return Task.CompletedTask;
    }

    public Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct = default)
    {
        _edges.Add(edge);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<GraphNode>> GetCallersAsync(string methodNodeId, CancellationToken ct = default)
    {
        var callerIds = _edges
            .Where(e => e.Type == EdgeType.Calls && e.ToId == methodNodeId)
            .Select(e => e.FromId);

        var callers = callerIds
            .Select(id => _nodes.TryGetValue(id, out var n) ? n : null)
            .OfType<GraphNode>()
            .ToList();

        return Task.FromResult<IReadOnlyList<GraphNode>>(callers);
    }

    public Task<IReadOnlyList<GraphNode>> GetCalleesAsync(string methodNodeId, CancellationToken ct = default)
    {
        var calleeIds = _edges
            .Where(e => e.Type == EdgeType.Calls && e.FromId == methodNodeId)
            .Select(e => e.ToId);

        var callees = calleeIds
            .Select(id => _nodes.TryGetValue(id, out var n) ? n : null)
            .OfType<GraphNode>()
            .ToList();

        return Task.FromResult<IReadOnlyList<GraphNode>>(callees);
    }

    public Task<GraphIndexSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var summary = new GraphIndexSummary(
            Projects: _nodes.Values.Count(n => n.Kind == NodeKind.Project),
            Files: _nodes.Values.Count(n => n.Kind == NodeKind.File),
            Types: _nodes.Values.Count(n => n.Kind == NodeKind.Type),
            Methods: _nodes.Values.Count(n => n.Kind == NodeKind.Method),
            Calls: _edges.Count(e => e.Type == EdgeType.Calls));

        return Task.FromResult(summary);
    }
}
