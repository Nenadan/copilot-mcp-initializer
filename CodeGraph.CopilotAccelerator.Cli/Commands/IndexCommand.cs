using CodeGraph.CopilotAccelerator.Analyzers.Roslyn;
using CodeGraph.CopilotAccelerator.Core;
using CodeGraph.CopilotAccelerator.Graph.Abstractions;
using CodeGraph.CopilotAccelerator.Graph.Local;
using CodeGraph.CopilotAccelerator.Indexer;
using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;

namespace CodeGraph.CopilotAccelerator.Cli.Commands;

public sealed class IndexCommand : AsyncCommand<IndexCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<solution>")]
        [Description("Path to the .sln file to index")]
        public required string SolutionPath { get; init; }

        [CommandOption("--callers <nodeId>")]
        [Description("After indexing, print all callers of this node ID")]
        public string? Callers { get; init; }

        [CommandOption("--callees <nodeId>")]
        [Description("After indexing, print all callees of this node ID")]
        public string? Callees { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.SolutionPath))
        {
            AnsiConsole.MarkupLine($"[red]Solution not found:[/] {settings.SolutionPath}");
            return 1;
        }

        var store = new InMemoryGraphStore();
        var loader = new SolutionLoader();
        var pipeline = new IndexingPipeline(loader, store);

        GraphIndexSummary summary = null!;

        await AnsiConsole.Status()
            .StartAsync("Indexing solution...", async ctx =>
            {
                ctx.Spinner(Spinner.Known.Dots);
                summary = await pipeline.RunAsync(settings.SolutionPath);
            });

        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Metric")
            .AddColumn(new TableColumn("Count").RightAligned());

        table.AddRow("Projects", summary.Projects.ToString());
        table.AddRow("Files", summary.Files.ToString());
        table.AddRow("Types", summary.Types.ToString());
        table.AddRow("Methods", summary.Methods.ToString());
        table.AddRow("[bold]Calls[/]", $"[bold]{summary.Calls}[/]");

        AnsiConsole.WriteLine();
        AnsiConsole.Write(table);

        if (settings.Callers is not null)
        {
            var callers = await store.GetCallersAsync(settings.Callers);
            PrintNodes($"Callers of [yellow]{settings.Callers}[/]", callers);
        }

        if (settings.Callees is not null)
        {
            var callees = await store.GetCalleesAsync(settings.Callees);
            PrintNodes($"Callees of [yellow]{settings.Callees}[/]", callees);
        }

        return 0;
    }

    private static void PrintNodes(string header, IReadOnlyList<GraphNode> nodes)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(header);

        if (nodes.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]  (none)[/]");
            return;
        }

        foreach (var node in nodes)
        {
            var location = node.FilePath is not null
                ? $" [grey]({Path.GetFileName(node.FilePath)}:{node.LineStart})[/]"
                : string.Empty;
            AnsiConsole.MarkupLine($"  [cyan]{node.FullName ?? node.Label}[/]{location}");
        }
    }
}
