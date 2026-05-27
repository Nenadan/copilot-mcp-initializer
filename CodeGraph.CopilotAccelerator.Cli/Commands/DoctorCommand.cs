using Microsoft.Build.Locator;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CodeGraph.CopilotAccelerator.Cli.Commands;

public sealed class DoctorCommand : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[bold]CodeGraph Doctor[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .BorderColor(Color.Grey)
            .AddColumn("Check")
            .AddColumn("Status")
            .AddColumn("Detail");

        Check(table, ".NET runtime",
            () => Environment.Version.ToString());

        Check(table, "MSBuild SDK",
            () =>
            {
                var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                if (instances.Count == 0)
                    throw new InvalidOperationException("No MSBuild instances found");
                var best = instances.OrderByDescending(i => i.Version).First();
                return $"{best.Name} {best.Version}";
            });

        AnsiConsole.Write(table);
        return 0;
    }

    private static void Check(Table table, string name, Func<string> probe)
    {
        try
        {
            var detail = probe();
            table.AddRow(name, "[green]OK[/]", detail);
        }
        catch (Exception ex)
        {
            table.AddRow(name, "[red]FAIL[/]", ex.Message);
        }
    }
}
