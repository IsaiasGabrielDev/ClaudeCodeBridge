# ClaudeCodeBridge

A .NET 8 class library that integrates any application with the **Claude Code CLI** via subprocess. It provides dependency injection, real-time streaming progress events, and a clean abstraction over the Claude agent execution model — so your application code stays decoupled from the AI layer.

## How it works

```
Your App  ──►  ClaudeAgentService  ──►  claude CLI (subprocess)
               (this library)            │
                    ▲                    ├── reads CLAUDE.md (your domain rules)
                    │                    ├── calls MCP tools (databases, APIs, files)
               OnProgress event          └── streams JSON results
```

All domain knowledge lives in **CLAUDE.md** inside your agent project directory. The library handles only subprocess I/O — which means you can update business rules without recompiling.

## Installation

```bash
dotnet add package ClaudeCodeBridge
```

Requires the [Claude Code CLI](https://claude.ai/code) installed and accessible on PATH (or configured via `ClaudeOptions.ClaudeExecutable`).

## Quick start

### 1. Configure via `appsettings.json`

```json
{
  "Claude": {
    "ClaudeExecutable": "claude",
    "ProjectDirectory": "C:\\MyAgentProject",
    "MaxTurns": 30,
    "Timeout": "00:20:00",
    "SkipPermissions": false
  }
}
```

### 2. Register in DI

```csharp
// ASP.NET Core / Generic Host
builder.Services.AddClaudeCodeBridge(builder.Configuration.GetSection("Claude"));

// WinForms / Console (no host)
var services = new ServiceCollection();
services.AddClaudeCodeBridge(opts =>
{
    opts.ProjectDirectory = @"C:\MyAgentProject";
});
var provider = services.BuildServiceProvider();
```

### 3. Use `IClaudeAgentService`

```csharp
public class MyController(IClaudeAgentService agent)
{
    public async Task RunAsync()
    {
        agent.OnProgress += msg => Console.WriteLine(msg);

        // Execute a task with context
        var result = await agent.ExecuteAsync(
            new AgentContext
            {
                Id          = "ORD-2024-001",
                Group       = "Customer A",
                ContentPath = @"C:\orders\2024\ORD-2024-001",
                Metadata    = new Dictionary<string, string> { ["Priority"] = "High" }
            },
            task: "Review this order and flag any compliance issues.");

        if (result.Success)
            Console.WriteLine(result.Content);   // markdown response
        else
            Console.WriteLine($"Error: {result.Error}");
    }
}
```

### Free-form query

```csharp
var result = await agent.QueryAsync(new AgentQuery
{
    Context  = new AgentContext { Id = "ORD-2024-001", Group = "Customer A" },
    Question = "What is the delivery status of this order?"
});
```

### Raw prompt (full control)

```csharp
var result = await agent.RunPromptAsync("Summarise the last 5 open issues in the database.");
```

## Configuration reference

| Property | Default | Description |
|---|---|---|
| `ClaudeExecutable` | `"claude"` | Path to the CLI binary |
| `ProjectDirectory` | `""` | Working dir — must contain `CLAUDE.md` |
| `McpConfigPath` | `null` | Custom `mcp.json` path (CLI default if null) |
| `MaxTurns` | `30` | Agent turn limit per call |
| `Timeout` | `00:20:00` | Hard timeout per call |
| `SkipPermissions` | `false` | Passes `--dangerously-skip-permissions` |
| `LogFilePath` | `null` | File path for verbose I/O log (disabled if null) |

## Customising tool labels

The `OnProgress` event includes tool-invocation messages. By default, standard Claude Code tools (Read, Write, Bash, etc.) are shown with readable labels. For custom MCP tools, replace the formatter:

```csharp
var svc = provider.GetRequiredService<IClaudeAgentService>();
((ClaudeAgentService)svc).ToolLabelFormatter = name => name switch
{
    "mcp__mydb__query"   => "Querying database...",
    "mcp__mydb__execute" => "Updating database...",
    _                    => ClaudeAgentService.DefaultToolLabel(name)
};
```

## Project structure expected at runtime

```
ProjectDirectory/
├── CLAUDE.md               ← domain rules, checklists, instructions
├── mcp.json                ← MCP server configuration (optional)
└── .claude/
    ├── settings.json
    └── skills/             ← reusable agent skills (optional)
```

## Design decisions

- **Subprocess over HTTP API** — uses the CLI directly, so MCP tools, local file access, and `CLAUDE.md` context work out of the box with no additional plumbing.
- **Separation of concerns** — business logic stays in `CLAUDE.md`; the library only manages I/O. Updating domain rules requires no recompilation.
- **Interface-first** — `IClaudeAgentService` makes the agent mockable in unit tests.
- **Configurable permissions** — `SkipPermissions` is opt-in and documented; not silently on.

## License

MIT
