# CodeGraph Copilot Accelerator - Architecture Document

## 1. Project Overview

### 1.1 Working Name

**CodeGraph Copilot Accelerator**

Alternative product names:

- CodeGraph MCP
- Architecture Context Engine for Copilot
- Local Code Knowledge Graph for Copilot

### 1.2 Purpose

The product builds a local, queryable architecture map of a .NET codebase and exposes that map to GitHub Copilot through an MCP server.

The goal is not to replace Copilot. The goal is to give Copilot better architectural context before it generates or modifies code.

### 1.3 Core Idea

```text
Codebase
  -> Roslyn-based scanner
  -> code knowledge graph
  -> MCP server
  -> GitHub Copilot
  -> better task-specific answers and code changes
```

### 1.4 Main Problem Solved

Large codebases are difficult for AI coding tools because important context is spread across controllers, handlers, services, entities, tests, configuration, dependency injection setup, and external integrations.

This solution extracts those relationships ahead of time and lets Copilot query them through high-level tools such as:

- `impact_analysis`
- `trace_flow`
- `find_related_tests`
- `find_entrypoints`
- `create_context_pack`
- `explain_architecture_area`

### 1.5 Target Users

Primary users:

- .NET backend developers
- Tech leads
- Solution architects
- Teams using GitHub Copilot in larger enterprise repositories

Initial target repository type:

- .NET 10 solution
- C#
- ASP.NET Core
- EF Core
- Dependency Injection
- Optional MediatR/CQRS
- Unit/integration tests

---

## 2. Direct Architectural Position

The solution should have two main runtime parts and one shared core.

```text
                  +--------------------+
                  |      Codebase      |
                  +---------+----------+
                            |
                            v
                  +--------------------+
                  |  CodeGraph Indexer |
                  | Roslyn + analyzers |
                  +---------+----------+
                            |
                            v
                  +--------------------+
                  |    Graph Store     |
                  | Local/Cosmos/etc.  |
                  +---------+----------+
                            |
                            v
+-------------+   +--------------------+
| GitHub      |<->| CodeGraph MCP      |
| Copilot     |   | Server             |
+-------------+   +--------------------+
```

### 2.1 Section 1 - CodeGraph Indexer

The indexer scans the codebase and writes graph data.

Responsibilities:

- Load `.sln` and `.csproj` files.
- Build Roslyn compilations.
- Analyze syntax and semantic models.
- Extract symbols, methods, types, references, calls, inheritance, attributes, and constructor dependencies.
- Detect ASP.NET Core endpoints.
- Detect controllers and actions.
- Detect minimal APIs.
- Detect DI registrations.
- Detect EF Core DbContexts, DbSets, entities, configurations, and migrations.
- Detect request/handler patterns such as MediatR.
- Detect tests and possible test coverage relationships.
- Store graph nodes and graph edges.
- Support incremental re-indexing later.

Initial commands:

```bash
codegraph index ./MySolution.sln
codegraph watch ./MySolution.sln
```

### 2.2 Section 2 - CodeGraph MCP Server

The MCP server is the interface between Copilot and the graph.

Responsibilities:

- Expose MCP tools to GitHub Copilot.
- Accept high-level tool calls.
- Query the graph store.
- Build focused context packs.
- Return relevant files, symbols, dependency paths, tests, and risks.
- Hide graph database complexity from Copilot.

The MCP server should not expose raw Gremlin or SQL as the primary interface. Copilot should call product-level tools.

Bad interface:

```text
query_graph("raw database query")
```

Better interface:

```text
impact_analysis("OrderStatus")
trace_flow("POST /orders")
find_related_tests("CreateOrderHandler")
create_context_pack("Add validation to CreateOrder")
```

### 2.3 Section 3 - Shared Core

The shared core is used by both the indexer and the MCP server.

Responsibilities:

- Graph abstractions
- Query model
- Context pack generation
- Ranking logic
- Source location model
- Configuration model
- Common DTOs
- Logging and diagnostics abstractions

---

## 3. Key Product Principle

The database is not the product.

The product is the ability to answer architecture-aware questions quickly and accurately.

The graph database is an implementation detail. The value is in:

1. Correct extraction of relationships.
2. Good graph model.
3. Good ranking.
4. Useful MCP tools.
5. Focused context packs for Copilot.

---

## 4. Tech Stack

### 4.1 Runtime and Language

| Area | Choice | Reason |
|---|---|---|
| Runtime | .NET 10 | Modern .NET baseline, suitable for long-lived tooling and enterprise .NET teams. |
| Language | C# | Native fit for Roslyn and .NET code analysis. |
| CLI | System.CommandLine or Spectre.Console.Cli | CLI-first local indexing experience. |
| Background processing | Hosted services | Useful for watch mode and long-running indexing. |
| Logging | Microsoft.Extensions.Logging + Serilog optional | Structured logs for diagnostics. |
| Configuration | Microsoft.Extensions.Configuration | Consistent .NET configuration model. |
| Testing | xUnit or NUnit | Standard .NET testing stack. |

### 4.2 Static Analysis

| Area | Choice | Reason |
|---|---|---|
| C# analysis | Roslyn | Access to syntax trees, semantic models, symbols, references, and compilations. |
| Project loading | Microsoft.CodeAnalysis.Workspaces.MSBuild | Load `.sln` and `.csproj` files. |
| Framework analyzers | Custom analyzer modules | Extract ASP.NET Core, EF Core, DI, CQRS, and test relationships. |

### 4.3 MCP Integration

| Area | Choice | Reason |
|---|---|---|
| MCP server | Official MCP C# SDK | Native .NET implementation path for exposing tools/resources to clients. |
| Local transport | stdio | Best default for local Copilot/IDE integration. |
| Remote/team transport | Streamable HTTP later | Useful for shared or hosted deployments. |
| Tool design | High-level architecture tools | Prevents Copilot from needing to understand raw database query syntax. |

### 4.4 Storage

| Area | Choice | Reason |
|---|---|---|
| Storage abstraction | Required | Prevents the architecture from being locked to Cosmos DB. |
| Default MVP store | Local embedded store | Faster setup, cross-platform, lower friction. |
| Cosmos Gremlin adapter | Optional | Useful for Azure-focused customers and demos. |
| Vector/search index | Optional stage 2+ | Useful for natural-language code discovery. |

Recommended initial local store options:

| Option | Fit |
|---|---|
| SQLite + normalized graph tables | Best simple MVP option. |
| LiteDB | Simple embedded document option. |
| KuzuDB | Strong local graph option if graph traversal becomes central. |
| Cosmos DB Gremlin | Good optional Azure adapter, not ideal as mandatory local default. |

### 4.5 Database Decision

Do not make Cosmos DB Emulator mandatory for local development.

Reason:

- Cosmos DB Gremlin is useful, but it introduces local setup friction.
- Cosmos DB Emulator support differs by API and emulator mode.
- The Docker emulator currently does not support the API for Apache Gremlin.
- A storage abstraction lets the product support local, cloud, and test storage without redesign.

Recommended abstraction:

```csharp
public interface ICodeGraphStore
{
    Task UpsertNodeAsync(GraphNode node, CancellationToken ct);
    Task UpsertEdgeAsync(GraphEdge edge, CancellationToken ct);
    Task<IReadOnlyList<GraphNode>> SearchNodesAsync(NodeSearchQuery query, CancellationToken ct);
    Task<IReadOnlyList<GraphPath>> TraverseAsync(GraphTraversalQuery query, CancellationToken ct);
    Task DeleteBySourceFileAsync(string filePath, CancellationToken ct);
}
```

---

## 5. Solution Structure

Recommended initial repository structure:

```text
src/
  CodeGraph.Cli/
  CodeGraph.Indexer/
  CodeGraph.McpServer/

  CodeGraph.Core/
  CodeGraph.QueryEngine/
  CodeGraph.ContextPacks/

  CodeGraph.Analyzers.Roslyn/
  CodeGraph.Analyzers.AspNetCore/
  CodeGraph.Analyzers.EfCore/
  CodeGraph.Analyzers.DependencyInjection/
  CodeGraph.Analyzers.Tests/
  CodeGraph.Analyzers.MediatR/

  CodeGraph.Graph.Abstractions/
  CodeGraph.Graph.Local/
  CodeGraph.Graph.CosmosGremlin/
  CodeGraph.Graph.InMemory/

tests/
  CodeGraph.Indexer.Tests/
  CodeGraph.QueryEngine.Tests/
  CodeGraph.McpServer.Tests/
  CodeGraph.IntegrationTests/

samples/
  Sample.ModularMonolith/
  Sample.CleanArchitecture/
  Sample.MinimalApi/

docs/
  architecture.md
  graph-schema.md
  mcp-tools.md
  adr/
```

---

## 6. Component Architecture

### 6.1 CLI

Project:

```text
CodeGraph.Cli
```

Responsibilities:

- Provide developer commands.
- Trigger indexing.
- Trigger watch mode.
- Start MCP server if needed.
- Run diagnostics.
- Export graph/debug data.

Commands:

```bash
codegraph index ./MySolution.sln
codegraph watch ./MySolution.sln
codegraph mcp-server --store ./.codegraph
codegraph ask "What changes if I modify OrderStatus?"
codegraph doctor
```

### 6.2 Indexer

Project:

```text
CodeGraph.Indexer
```

Responsibilities:

- Orchestrate indexing pipeline.
- Load solution.
- Run analyzers.
- Deduplicate nodes/edges.
- Persist graph mutations.
- Track file hashes and incremental state.

Indexer pipeline:

```text
Solution path
  -> workspace load
  -> project graph extraction
  -> Roslyn compilation
  -> analyzer execution
  -> node/edge normalization
  -> graph persistence
  -> index summary
```

### 6.3 Roslyn Analyzer Layer

Project:

```text
CodeGraph.Analyzers.Roslyn
```

Responsibilities:

- Extract namespaces, types, methods, properties, fields.
- Resolve symbols.
- Resolve method invocations.
- Resolve interface implementations.
- Resolve inheritance.
- Resolve attributes.
- Extract source locations.

Core outputs:

- `ProjectNode`
- `FileNode`
- `NamespaceNode`
- `TypeNode`
- `MethodNode`
- `PropertyNode`
- `CALLS` edges
- `DECLARES` edges
- `IMPLEMENTS` edges
- `INHERITS` edges

### 6.4 Framework Analyzer Layer

Projects:

```text
CodeGraph.Analyzers.AspNetCore
CodeGraph.Analyzers.EfCore
CodeGraph.Analyzers.DependencyInjection
CodeGraph.Analyzers.MediatR
CodeGraph.Analyzers.Tests
```

Responsibilities:

- Add higher-level application relationships on top of Roslyn symbols.

Examples:

ASP.NET Core analyzer:

- Detect controllers.
- Detect actions.
- Detect `[Route]`, `[HttpGet]`, `[HttpPost]`, etc.
- Detect minimal API calls such as `MapGet`, `MapPost`, `MapGroup`.
- Link routes to handlers or methods.

EF Core analyzer:

- Detect `DbContext` classes.
- Detect `DbSet<T>` properties.
- Detect entity types.
- Detect `IEntityTypeConfiguration<T>`.
- Detect migrations.
- Detect `SaveChanges` and `SaveChangesAsync` callers.

Dependency Injection analyzer:

- Detect `AddScoped`, `AddTransient`, `AddSingleton`.
- Detect constructor-injected dependencies.
- Link interfaces to implementations where possible.

MediatR/CQRS analyzer:

- Detect `IRequest<T>`.
- Detect `IRequestHandler<TRequest, TResponse>`.
- Detect `INotification`.
- Detect `INotificationHandler<TNotification>`.
- Detect `Send` and `Publish` call sites.

Test analyzer:

- Detect xUnit, NUnit, and MSTest tests.
- Detect test classes and test methods.
- Link tests to target symbols using naming conventions, direct calls, and references.

### 6.5 Graph Store

Projects:

```text
CodeGraph.Graph.Abstractions
CodeGraph.Graph.Local
CodeGraph.Graph.CosmosGremlin
CodeGraph.Graph.InMemory
```

Responsibilities:

- Store nodes and edges.
- Support search.
- Support traversal.
- Support incremental updates.
- Provide query-optimized access patterns.

Important rule:

The rest of the application should not know if the backing store is SQLite, Kuzu, Cosmos Gremlin, or something else.

### 6.6 Query Engine

Project:

```text
CodeGraph.QueryEngine
```

Responsibilities:

- Convert product-level questions into graph queries.
- Rank results.
- Expand context around relevant nodes.
- Find paths between nodes.
- Produce query results used by MCP tools.

Example query methods:

```csharp
Task<ImpactAnalysisResult> AnalyzeImpactAsync(SymbolQuery query, CancellationToken ct);
Task<FlowTraceResult> TraceFlowAsync(EntryPointQuery query, CancellationToken ct);
Task<TestDiscoveryResult> FindRelatedTestsAsync(SymbolQuery query, CancellationToken ct);
Task<ArchitectureAreaResult> ExplainAreaAsync(AreaQuery query, CancellationToken ct);
```

### 6.7 Context Pack Generator

Project:

```text
CodeGraph.ContextPacks
```

Responsibilities:

- Convert graph/query results into concise AI-consumable context.
- Include only useful files, snippets, symbols, and relationships.
- Add confidence and risk notes.
- Avoid overwhelming Copilot with raw graph data.

A context pack should include:

```text
- Task interpretation
- Relevant entrypoints
- Relevant domain/application/infrastructure files
- Dependency path
- Tests to update/run
- Existing architectural conventions
- Risk notes
- Confidence notes
```

### 6.8 MCP Server

Project:

```text
CodeGraph.McpServer
```

Responsibilities:

- Expose MCP tools.
- Call QueryEngine.
- Return context packs and structured results.
- Support stdio for local usage.
- Support HTTP transport later if required.

Initial MCP tools:

```text
codegraph.create_context_pack
codegraph.impact_analysis
codegraph.trace_flow
codegraph.find_entrypoints
codegraph.find_related_tests
codegraph.explain_architecture_area
codegraph.search_symbols
```

---

## 7. Graph Model

### 7.1 Node Types

Minimum node types:

| Node Type | Description |
|---|---|
| Repository | Git repository or workspace root. |
| Solution | `.sln` file. |
| Project | `.csproj` file. |
| Package | NuGet package dependency. |
| File | Source/config/test file. |
| Namespace | C# namespace. |
| Type | Class, record, struct, interface, enum. |
| Method | Method, constructor, local function where relevant. |
| Property | Property or indexer. |
| Endpoint | HTTP endpoint. |
| ControllerAction | MVC/Web API action. |
| Handler | Request/event/command/query handler. |
| Entity | Domain or EF Core entity. |
| DbContext | EF Core DbContext. |
| DbSet | EF Core DbSet. |
| ConfigKey | Configuration key. |
| ExternalResource | Database, queue, topic, API, cache, storage. |
| Test | Test method or test class. |
| Chunk | Source snippet used for context packs. |

### 7.2 Edge Types

Minimum edge types:

| Edge | Meaning |
|---|---|
| CONTAINS | Parent contains child. |
| DECLARES | File declares symbol. |
| CALLS | Method invokes another method. |
| IMPLEMENTS | Type implements interface. |
| INHERITS | Type derives from type. |
| INJECTS | Constructor depends on service. |
| REGISTERS | DI setup registers implementation. |
| ROUTES_TO | Endpoint routes to action/handler. |
| HANDLES | Handler handles command/query/event. |
| READS | Code reads config/entity/resource. |
| WRITES | Code writes entity/resource. |
| PUBLISHES | Code publishes event/message. |
| SUBSCRIBES | Code consumes event/message. |
| MAPS_TO | DTO maps to entity/view model. |
| TESTS | Test covers method/type/endpoint. |
| DEPENDS_ON | Project/package dependency. |

### 7.3 Common Node Properties

```text
id
kind
name
fullName
signature
filePath
lineStart
lineEnd
project
language
hash
lastIndexedCommit
summary
confidence
metadataVersion
```

### 7.4 Confidence Model

Every inferred relationship should have a confidence value.

Examples:

| Relationship | Confidence |
|---|---|
| Roslyn-resolved method call | High |
| Interface implementation | High |
| Attribute route to controller action | High |
| Minimal API delegate target | Medium to high |
| DI interface to implementation from explicit registration | High |
| DI interface to implementation from assembly scanning | Medium |
| Test to production code by naming convention | Low to medium |
| Runtime reflection relationship | Low |

---

## 8. Main Data Flows

### 8.1 Indexing Flow

```text
Developer runs index command
  -> CLI validates solution path
  -> Indexer opens solution
  -> Roslyn builds project compilations
  -> Core analyzers extract symbols and references
  -> Framework analyzers extract architectural relationships
  -> Indexer normalizes graph mutations
  -> Graph store persists nodes and edges
  -> Index summary is printed
```

### 8.2 Copilot Query Flow

```text
Developer asks Copilot a task
  -> Copilot calls CodeGraph MCP tool
  -> MCP server receives tool input
  -> QueryEngine searches/traverses graph
  -> ContextPack generator builds concise result
  -> MCP server returns structured answer
  -> Copilot uses returned context to answer or edit code
```

### 8.3 Incremental Update Flow

```text
File changed
  -> Watcher detects change
  -> Indexer calculates file hash
  -> Affected project is re-analyzed
  -> Old nodes/edges for changed file are removed or marked stale
  -> New nodes/edges are written
  -> Graph version is updated
```

---

## 9. MCP Tool Contracts

### 9.1 `codegraph.create_context_pack`

Purpose:

Create a focused context package for a development task.

Input:

```json
{
  "task": "Add validation to CreateOrder",
  "maxFiles": 12,
  "includeTests": true
}
```

Output:

```json
{
  "summary": "CreateOrder flow uses controller -> command -> validator -> handler -> DbContext.",
  "files": [
    "src/Orders.Api/Controllers/OrdersController.cs",
    "src/Orders.Application/CreateOrder/CreateOrderCommand.cs",
    "src/Orders.Application/CreateOrder/CreateOrderValidator.cs",
    "src/Orders.Application/CreateOrder/CreateOrderHandler.cs",
    "tests/Orders.Application.Tests/CreateOrderTests.cs"
  ],
  "symbols": [
    "OrdersController.Create",
    "CreateOrderCommand",
    "CreateOrderValidator",
    "CreateOrderHandler.Handle"
  ],
  "conventions": [
    "Commands are records",
    "Validation uses FluentValidation",
    "Handlers return Result<T>"
  ],
  "risks": [
    "Validation errors may affect API response contract"
  ]
}
```

### 9.2 `codegraph.impact_analysis`

Purpose:

Show what may break or need updates when a symbol/file changes.

Input:

```json
{
  "target": "OrderStatus",
  "depth": 3,
  "includeTests": true
}
```

Output should include:

- Direct references
- Indirect callers
- endpoints affected
- handlers affected
- tests affected
- configuration/resources affected
- confidence notes

### 9.3 `codegraph.trace_flow`

Purpose:

Trace an execution path from an entrypoint.

Input:

```json
{
  "entrypoint": "POST /orders",
  "maxDepth": 6
}
```

Output should include:

```text
POST /orders
  -> OrdersController.Create
  -> CreateOrderCommand
  -> CreateOrderValidator
  -> CreateOrderHandler.Handle
  -> OrdersDbContext.Orders
  -> OrderCreatedDomainEvent
```

### 9.4 `codegraph.find_related_tests`

Purpose:

Find tests relevant to a symbol, file, endpoint, or feature.

Input:

```json
{
  "target": "CreateOrderHandler"
}
```

Output should include:

- test files
- test methods
- relationship confidence
- reason for matching

---

## 10. Implementation Stages

## Stage 0 - Foundation and Decisions

Goal:

Create the solution skeleton and lock core architecture decisions.

Deliverables:

- Repository structure.
- Initial `.NET 10` solution.
- CLI project.
- MCP server project.
- Indexer project.
- Core abstractions.
- Graph store abstraction.
- In-memory graph implementation for tests.
- ADR documents.

Decisions:

- Use .NET 10 and C#.
- Use Roslyn as the primary static analysis engine.
- Use MCP as the Copilot integration layer.
- Use storage abstraction from day one.
- Do not make Cosmos DB mandatory for MVP.
- CLI-first developer experience.

Exit criteria:

- `dotnet build` passes.
- Empty CLI command runs.
- Empty MCP server starts.
- Graph abstraction has tests.

---

## Stage 1 - Roslyn MVP Indexer

Goal:

Index the basic structure of a C# solution.

Deliverables:

- Load `.sln` and projects.
- Extract projects, files, namespaces, types, methods, properties.
- Extract inheritance and interface implementation.
- Extract direct method calls where Roslyn can resolve target symbols.
- Store graph in local embedded store or in-memory store.
- CLI command: `codegraph index`.
- CLI command: `codegraph doctor`.

Graph support:

- Repository
- Solution
- Project
- File
- Namespace
- Type
- Method
- Property
- CONTAINS
- DECLARES
- CALLS
- IMPLEMENTS
- INHERITS
- DEPENDS_ON

Exit criteria:

- Can index a sample solution.
- Can print summary: projects, files, types, methods, calls.
- Can query direct callers/callees from CLI.

---

## Stage 2 - Framework Analyzers

Goal:

Extract useful application architecture, not just C# structure.

Deliverables:

- ASP.NET Core analyzer.
- Minimal API analyzer.
- Controller/action analyzer.
- DI analyzer.
- EF Core analyzer.
- MediatR/CQRS analyzer.
- Test analyzer.

Graph support:

- Endpoint
- ControllerAction
- Handler
- Entity
- DbContext
- DbSet
- Test
- ROUTES_TO
- HANDLES
- INJECTS
- REGISTERS
- READS
- WRITES
- TESTS

Exit criteria:

- Can trace `POST /orders` to controller/handler/db context in sample app.
- Can find likely tests for a handler or endpoint.
- Can show dependencies injected into a service.

---

## Stage 3 - Query Engine

Goal:

Build product-level queries on top of the graph.

Deliverables:

- `impact_analysis` query.
- `trace_flow` query.
- `find_entrypoints` query.
- `find_related_tests` query.
- Symbol/file search.
- Result ranking.
- Confidence scoring.

Exit criteria:

- CLI can answer:

```bash
codegraph ask "What changes if I modify OrderStatus?"
codegraph ask "Trace POST /orders"
codegraph ask "Find tests for CreateOrderHandler"
```

- Results are useful without needing raw database queries.

---

## Stage 4 - Context Pack Generator

Goal:

Return concise task-specific context suitable for Copilot.

Deliverables:

- Context pack model.
- File ranking.
- Symbol ranking.
- Snippet extraction.
- Conventions extraction.
- Risk notes.
- Token budget support.

Context pack sections:

```text
Task
Relevant flow
Files to inspect/change
Symbols
Tests
Conventions
Risks
Confidence notes
```

Exit criteria:

- For a task like `Add validation to CreateOrder`, the system returns 5-12 relevant files instead of the whole repository.
- Context pack is readable by both humans and AI tools.

---

## Stage 5 - MCP Server MVP

Goal:

Expose the graph and context packs to GitHub Copilot through MCP.

Deliverables:

- MCP server with stdio transport.
- Tool: `codegraph.create_context_pack`.
- Tool: `codegraph.impact_analysis`.
- Tool: `codegraph.trace_flow`.
- Tool: `codegraph.find_related_tests`.
- Tool: `codegraph.search_symbols`.
- Local MCP configuration example.
- MCP server logging and diagnostics.

Exit criteria:

- Copilot can call MCP tools.
- MCP server returns context packs from the local graph store.
- No full re-indexing happens during a normal Copilot query.

---

## Stage 6 - Incremental Indexing and Watch Mode

Goal:

Keep the graph fresh during development.

Deliverables:

- File watcher.
- File hash tracking.
- Project-level invalidation.
- Changed-file re-analysis.
- Stale node/edge cleanup.
- CLI command: `codegraph watch`.

Exit criteria:

- Editing a file updates related graph data without re-indexing the whole solution.
- MCP queries use the latest indexed data.

---

## Stage 7 - Cosmos Gremlin Adapter

Goal:

Add Cosmos DB Gremlin support as an optional storage backend.

Deliverables:

- `CodeGraph.Graph.CosmosGremlin` project.
- Gremlin query translator for required operations.
- Cosmos-specific schema constraints.
- Setup documentation.
- Integration tests where environment supports it.

Decision:

Cosmos Gremlin is an adapter, not the core architecture.

Exit criteria:

- Same QueryEngine works against local store and Cosmos adapter.
- Cosmos-specific limitations are isolated in the adapter.

---

## Stage 8 - Vector/Search Enhancement

Goal:

Improve natural-language discovery.

Deliverables:

- Source chunking.
- Symbol summaries.
- Optional embedding generation.
- Hybrid retrieval: vector search first, graph expansion second.
- Query ranking improvements.

Retrieval flow:

```text
User task
  -> semantic search for candidate concepts
  -> graph expansion around candidates
  -> rank files/symbols
  -> generate context pack
  -> return through MCP
```

Exit criteria:

- Queries like `Where is customer onboarding implemented?` return useful results even when symbol names differ.

---

## Stage 9 - Hardening and Team Use

Goal:

Make the tool reliable for real teams.

Deliverables:

- Performance profiling.
- Large solution benchmarks.
- Redaction/secrets checks.
- Configurable ignored paths.
- CI indexing validation.
- Graph export/import.
- Versioned graph schema.
- Documentation.

Exit criteria:

- Works on large repositories.
- Has clear failure messages.
- Can be adopted by another developer without manual setup help.

---

## 11. Architecture Decisions

### ADR-001 - Use Roslyn for .NET Code Analysis

Decision:

Use Roslyn as the primary engine for C# code understanding.

Reason:

- Roslyn exposes compiler-level syntax and semantic information.
- Symbol resolution is more reliable than text parsing.
- It supports method calls, declarations, inheritance, interface implementation, and source locations.

Consequences:

- The first-class language is C#.
- Non-.NET languages require separate analyzers later.

### ADR-002 - Use MCP for Copilot Integration

Decision:

Expose CodeGraph to Copilot through MCP tools.

Reason:

- MCP is designed for exposing tools and data sources to AI assistants.
- It keeps Copilot integration clean.
- It avoids creating a custom Copilot-specific protocol.

Consequences:

- MCP tool design becomes a core product concern.
- Tool outputs must be concise and structured.

### ADR-003 - Use a Storage Abstraction

Decision:

All graph storage must go through `ICodeGraphStore`.

Reason:

- Avoids locking the architecture to Cosmos DB.
- Enables local embedded storage for MVP.
- Enables in-memory tests.
- Enables Cosmos, Neo4j, Kuzu, or other stores later.

Consequences:

- The first version needs careful abstraction design.
- QueryEngine should express product queries, not database-specific queries.

### ADR-004 - Do Not Make Cosmos DB Emulator Mandatory

Decision:

Cosmos DB Gremlin support is optional.

Reason:

- Local emulator support and developer setup can be platform-sensitive.
- Mandatory Cosmos would slow MVP adoption.
- The graph model should outlive any single storage backend.

Consequences:

- The MVP should use a simpler default local store.
- Cosmos Gremlin becomes a later adapter.

### ADR-005 - CLI First, VS Code Extension Later

Decision:

Build CLI and MCP server first. Add VS Code extension later only if needed.

Reason:

- CLI is easier to test and automate.
- MCP server already gives Copilot integration.
- VS Code extension can become UI polish after core value is proven.

Consequences:

- Initial UX is command-line based.
- Documentation must provide simple setup commands.

---

## 12. MVP Scope

### 12.1 Include in MVP

- .NET 10 solution.
- C# static analysis using Roslyn.
- CLI indexing command.
- Local graph store.
- Basic graph query engine.
- ASP.NET Core endpoint detection.
- Controller/action detection.
- DI detection.
- EF Core detection.
- Test discovery.
- MCP server with stdio.
- Context pack generation.

### 12.2 Exclude from MVP

- Full VS Code extension.
- Full cloud SaaS mode.
- Full TypeScript analysis.
- Full Terraform/Bicep/Kubernetes analysis.
- Perfect runtime behavior analysis.
- Full graph visualization UI.
- Mandatory Cosmos DB.
- LLM-generated architecture as source of truth.

---

## 13. Non-Functional Requirements

### 13.1 Performance

Initial target:

- Small solution: under 30 seconds for initial index.
- Medium solution: under 2 minutes for initial index.
- Incremental file update: under 5 seconds.
- MCP query response: ideally under 2 seconds for common queries.

These are target numbers and should be benchmarked against real repositories.

### 13.2 Reliability

Requirements:

- Indexing should not fail the whole run because one project cannot compile.
- Partial results should be stored with diagnostics.
- Analyzer errors should be isolated.
- Graph schema should be versioned.

### 13.3 Security

Requirements:

- Default mode is local-only.
- Do not send source code to external services by default.
- Avoid indexing secrets from `.env`, `appsettings.*`, and local config files unless explicitly enabled.
- Support ignored paths.
- MCP server should expose only the current workspace graph.

### 13.4 Observability

Requirements:

- Structured logs.
- Indexing summary.
- Analyzer diagnostics.
- Slow query logging.
- `codegraph doctor` command.

---

## 14. Initial Configuration File

Recommended config file:

```yaml
version: 1
solution: ./MySolution.sln
store:
  provider: local
  path: ./.codegraph
indexing:
  include:
    - src/**
    - tests/**
  exclude:
    - bin/**
    - obj/**
    - .git/**
    - node_modules/**
    - appsettings.Development.json
    - .env
analyzers:
  aspnetcore: true
  efcore: true
  dependencyInjection: true
  mediatr: true
  tests: true
mcp:
  transport: stdio
contextPacks:
  maxFiles: 12
  maxSnippets: 20
  includeTests: true
```

---

## 15. Example End-to-End Scenario

Developer asks Copilot:

```text
Add validation to CreateOrder.
```

Copilot calls:

```text
codegraph.create_context_pack
```

CodeGraph returns:

```text
Relevant flow:
POST /orders
  -> OrdersController.Create
  -> CreateOrderCommand
  -> CreateOrderValidator
  -> CreateOrderHandler.Handle
  -> OrdersDbContext.Orders

Likely files:
- src/Orders.Api/Controllers/OrdersController.cs
- src/Orders.Application/CreateOrder/CreateOrderCommand.cs
- src/Orders.Application/CreateOrder/CreateOrderValidator.cs
- src/Orders.Application/CreateOrder/CreateOrderHandler.cs
- tests/Orders.Application.Tests/CreateOrderTests.cs

Existing conventions:
- Commands are records.
- Validation uses FluentValidation.
- Handlers return Result<T>.
- Tests use xUnit.

Risks:
- API validation response shape may change.
- Existing tests may assert exact error messages.
```

Copilot then edits code using focused project context instead of guessing from isolated files.

---

## 16. Main Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Static analysis misses runtime behavior | Incorrect graph | Add confidence scoring and diagnostics. |
| Graph becomes too noisy | Poor Copilot output | Store architectural entities, not every syntax node. |
| Cosmos Gremlin setup slows adoption | Bad developer experience | Make Cosmos optional. Use local default store. |
| MCP tools return too much data | Copilot gets confused | Use context pack limits and ranking. |
| Large repos index slowly | Poor usability | Add incremental indexing and file hashing. |
| Framework conventions vary by team | Incomplete extraction | Analyzer plugin model and config. |
| Tests are hard to link accurately | Low confidence results | Combine naming, references, direct calls, and user overrides. |

---

## 17. Open Questions

These should be decided during Stage 0 or Stage 1:

1. What is the default local graph store: SQLite, LiteDB, or Kuzu?
2. Should the indexer require projects to compile, or allow best-effort partial indexing?
3. Should context packs include raw code snippets in MVP or only file/symbol references?
4. How will MCP configuration be distributed to developers?
5. Should indexing run manually, in watch mode, or automatically when MCP server starts?
6. What sample architecture should be used for validation: Clean Architecture, modular monolith, vertical slice, or all three?
7. Which test framework should be supported first: xUnit, NUnit, or MSTest?
8. Should package dependency analysis include transitive NuGet dependencies in MVP?

---

## 18. Recommended First Milestone

Build this first:

```bash
codegraph index ./Sample.CleanArchitecture.sln
codegraph ask "Trace POST /orders"
codegraph ask "Find tests for CreateOrderHandler"
codegraph mcp-server
```

Minimum successful demo:

1. Index a sample .NET solution.
2. Detect endpoint to handler to DbContext flow.
3. Detect related tests.
4. Start MCP server.
5. Copilot calls `create_context_pack`.
6. Copilot receives a focused architecture context pack.

---

## 19. Reference Notes

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- Roslyn SDK: https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/
- GitHub Copilot MCP tutorial: https://docs.github.com/en/copilot/tutorials/enhance-agent-mode-with-mcp
- MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- MCP transports: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports
- Azure Cosmos DB emulator: https://learn.microsoft.com/en-us/azure/cosmos-db/how-to-develop-emulator

