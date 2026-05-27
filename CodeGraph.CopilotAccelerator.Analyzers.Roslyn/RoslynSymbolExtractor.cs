using System.Runtime.CompilerServices;
using CodeGraph.CopilotAccelerator.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeGraph.CopilotAccelerator.Analyzers.Roslyn;

public sealed class RoslynSymbolExtractor(Project project)
{
    public async IAsyncEnumerable<GraphNode> ExtractNodesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return new GraphNode
        {
            Id = project.Name.ToProjectNodeId(),
            Kind = NodeKind.Project,
            Label = project.Name,
            FullName = project.Name,
            Language = project.Language
        };

        foreach (var document in project.Documents)
        {
            ct.ThrowIfCancellationRequested();
            if (document.FilePath is null) continue;

            var root = await document.GetSyntaxRootAsync(ct);
            var model = await document.GetSemanticModelAsync(ct);
            if (root is null || model is null) continue;

            yield return new GraphNode
            {
                Id = document.FilePath.ToFileNodeId(project.Name),
                Kind = NodeKind.File,
                Label = Path.GetFileName(document.FilePath),
                FullName = document.FilePath,
                FilePath = document.FilePath,
                Project = project.Name,
                Language = project.Language
            };

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol) continue;

                yield return new GraphNode
                {
                    Id = typeSymbol.ToNodeId(),
                    Kind = NodeKind.Type,
                    Label = typeSymbol.Name,
                    FullName = typeSymbol.ToDisplayString(),
                    FilePath = document.FilePath,
                    LineStart = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    LineEnd = typeDecl.GetLocation().GetLineSpan().EndLinePosition.Line + 1,
                    Project = project.Name,
                    Language = project.Language
                };

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && !method.IsImplicitlyDeclared)
                    {
                        var loc = method.Locations.FirstOrDefault();
                        var span = loc?.GetLineSpan();
                        yield return new GraphNode
                        {
                            Id = method.ToNodeId(),
                            Kind = NodeKind.Method,
                            Label = method.Name,
                            FullName = method.ToDisplayString(),
                            FilePath = document.FilePath,
                            LineStart = span?.StartLinePosition.Line + 1,
                            LineEnd = span?.EndLinePosition.Line + 1,
                            Project = project.Name,
                            Language = project.Language
                        };
                    }
                    else if (member is IPropertySymbol property)
                    {
                        var loc = property.Locations.FirstOrDefault();
                        var span = loc?.GetLineSpan();
                        yield return new GraphNode
                        {
                            Id = property.ToNodeId(),
                            Kind = NodeKind.Property,
                            Label = property.Name,
                            FullName = property.ToDisplayString(),
                            FilePath = document.FilePath,
                            LineStart = span?.StartLinePosition.Line + 1,
                            LineEnd = span?.EndLinePosition.Line + 1,
                            Project = project.Name,
                            Language = project.Language
                        };
                    }
                }
            }
        }
    }

    public async IAsyncEnumerable<GraphEdge> ExtractEdgesAsync(
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var projectId = project.Name.ToProjectNodeId();

        foreach (var document in project.Documents)
        {
            ct.ThrowIfCancellationRequested();
            if (document.FilePath is null) continue;

            var root = await document.GetSyntaxRootAsync(ct);
            var model = await document.GetSemanticModelAsync(ct);
            if (root is null || model is null) continue;

            var fileId = document.FilePath.ToFileNodeId(project.Name);

            yield return new GraphEdge { FromId = projectId, ToId = fileId, Type = EdgeType.Contains };

            foreach (var typeDecl in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (model.GetDeclaredSymbol(typeDecl) is not INamedTypeSymbol typeSymbol) continue;
                var typeId = typeSymbol.ToNodeId();

                yield return new GraphEdge { FromId = fileId, ToId = typeId, Type = EdgeType.Declares };

                if (typeSymbol.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
                    yield return new GraphEdge { FromId = typeId, ToId = baseType.ToNodeId(), Type = EdgeType.Inherits };

                foreach (var iface in typeSymbol.Interfaces)
                    yield return new GraphEdge { FromId = typeId, ToId = iface.ToNodeId(), Type = EdgeType.Implements };

                foreach (var member in typeSymbol.GetMembers())
                {
                    if (member is IMethodSymbol method && !method.IsImplicitlyDeclared)
                        yield return new GraphEdge { FromId = typeId, ToId = method.ToNodeId(), Type = EdgeType.Declares };
                    else if (member is IPropertySymbol property)
                        yield return new GraphEdge { FromId = typeId, ToId = property.ToNodeId(), Type = EdgeType.Declares };
                }
            }

            // CALLS edges — walk all invocation expressions and resolve caller/callee
            foreach (var inv in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var callerSymbol = model.GetEnclosingSymbol(inv.SpanStart) as IMethodSymbol;
                var calleeSymbol = model.GetSymbolInfo(inv).Symbol as IMethodSymbol;
                if (callerSymbol is not null && calleeSymbol is not null)
                    yield return new GraphEdge
                    {
                        FromId = callerSymbol.ToNodeId(),
                        ToId = calleeSymbol.ToNodeId(),
                        Type = EdgeType.Calls
                    };
            }
        }
    }
}
