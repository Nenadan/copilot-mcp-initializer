using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeGraph.CopilotAccelerator.Analyzers.Roslyn;

public sealed class SolutionLoader
{
    private static int _registered;

    // Call this once at process startup, before any MSBuild types are loaded.
    // On Snap-installed .NET, DOTNET_ROOT must be set before hostfxr initialises.
    public static void EnsureDotnetRootIsSet()
    {
        if (Environment.GetEnvironmentVariable("DOTNET_ROOT") is not null) return;

        string[] candidateRoots =
        [
            "/var/snap/dotnet/common/dotnet",   // Snap on Ubuntu/WSL
            "/usr/share/dotnet",                // Standard Linux install
            "/usr/local/share/dotnet",          // Homebrew on macOS
        ];

        foreach (var root in candidateRoots)
        {
            if (Directory.Exists(Path.Combine(root, "sdk")))
            {
                Environment.SetEnvironmentVariable("DOTNET_ROOT", root);
                return;
            }
        }
    }

    public async Task<Solution> LoadAsync(string solutionPath, CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _registered, 1) == 0)
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Could not locate an MSBuild SDK. Ensure DOTNET_ROOT points to your " +
                    ".NET SDK root, or call SolutionLoader.EnsureDotnetRootIsSet() before " +
                    "this method. Install .NET from https://aka.ms/dotnet/download", ex);
            }
        }

        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e =>
            Console.Error.WriteLine($"[workspace] {e.Diagnostic.Kind}: {e.Diagnostic.Message}"));

        return await workspace.OpenSolutionAsync(solutionPath, cancellationToken: ct);
    }
}
