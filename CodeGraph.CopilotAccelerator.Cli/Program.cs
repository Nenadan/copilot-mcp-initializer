using CodeGraph.CopilotAccelerator.Analyzers.Roslyn;
using CodeGraph.CopilotAccelerator.Cli.Commands;
using Spectre.Console.Cli;

// Must run before any MSBuild/Roslyn types are loaded so hostfxr finds the SDK
SolutionLoader.EnsureDotnetRootIsSet();

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("codegraph");

    config.AddCommand<IndexCommand>("index")
        .WithDescription("Index a .NET solution and print a graph summary");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check that the environment is configured correctly");
});

return app.Run(args);
