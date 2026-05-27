using CodeGraph.CopilotAccelerator.Analyzers.Roslyn;
using CodeGraph.CopilotAccelerator.Core;
using CodeGraph.CopilotAccelerator.Graph.Abstractions;

namespace CodeGraph.CopilotAccelerator.Indexer;

public sealed class IndexingPipeline(SolutionLoader loader, ICodeGraphStore store)
{
    public async Task<GraphIndexSummary> RunAsync(string solutionPath, CancellationToken ct = default)
    {
        var solution = await loader.LoadAsync(solutionPath, ct);

        var solutionName = Path.GetFileNameWithoutExtension(solutionPath);
        await store.UpsertNodeAsync(new GraphNode
        {
            Id = $"solution::{solutionName}",
            Kind = NodeKind.Solution,
            Label = solutionName,
            FullName = solutionPath
        }, ct);

        foreach (var project in solution.Projects)
        {
            ct.ThrowIfCancellationRequested();
            var extractor = new RoslynSymbolExtractor(project);

            await foreach (var node in extractor.ExtractNodesAsync(ct))
                await store.UpsertNodeAsync(node, ct);

            await foreach (var edge in extractor.ExtractEdgesAsync(ct))
                await store.UpsertEdgeAsync(edge, ct);
        }

        return await store.GetSummaryAsync(ct);
    }
}
