using Microsoft.CodeAnalysis;

namespace CodeGraph.CopilotAccelerator.Analyzers.Roslyn;

public static class SymbolExtensions
{
    public static string ToNodeId(this ISymbol symbol) => symbol switch
    {
        IMethodSymbol m => $"method::{m.ContainingAssembly?.Name}::{m.ContainingType?.ToDisplayString()}::{m.Name}({string.Join(",", m.Parameters.Select(p => p.Type.ToDisplayString()))})",
        INamedTypeSymbol t => $"type::{t.ContainingAssembly?.Name}::{t.ToDisplayString()}",
        IPropertySymbol p => $"property::{p.ContainingAssembly?.Name}::{p.ContainingType?.ToDisplayString()}::{p.Name}",
        INamespaceSymbol ns => $"namespace::{ns.ToDisplayString()}",
        _ => $"symbol::{symbol.ToDisplayString()}"
    };

    public static string ToProjectNodeId(this string projectName) =>
        $"project::{projectName}";

    public static string ToFileNodeId(this string filePath, string projectName) =>
        $"file::{projectName}::{filePath}";
}
