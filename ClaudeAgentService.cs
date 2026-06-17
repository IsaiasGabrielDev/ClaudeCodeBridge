using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ClaudeCodeBridge;

/// <summary>
/// Integrates any .NET application with the Claude Code CLI via subprocess.
/// All domain knowledge lives in CLAUDE.md — this service handles only I/O.
/// </summary>
public sealed class ClaudeAgentService : IClaudeAgentService
{
    private readonly ClaudeOptions _opts;
    private readonly ILogger<ClaudeAgentService> _logger;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Raised on a background thread each time the agent emits text or invokes a tool.
    /// Use <c>Invoke</c> / <c>BeginInvoke</c> when updating UI from this event.
    /// </summary>
    public event Action<string>? OnProgress;

    /// <summary>
    /// Overrides how tool names are displayed in <see cref="OnProgress"/> messages.
    /// Defaults to <see cref="DefaultToolLabel"/>. Replace to localise or add MCP names.
    /// </summary>
    public Func<string, string> ToolLabelFormatter { get; set; } = DefaultToolLabel;

    /// <summary>Initializes the service with options and a logger (resolved by DI).</summary>
    public ClaudeAgentService(IOptions<ClaudeOptions> options, ILogger<ClaudeAgentService> logger)
    {
        _opts = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<AgentResult> ExecuteAsync(
        AgentContext context, string task, CancellationToken ct = default)
    {
        var prompt = PromptBuilder.Task(context, task);
        return RunCoreAsync(prompt, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<AgentResult> QueryAsync(
        AgentQuery query, CancellationToken ct = default)
    {
        var prompt = PromptBuilder.Query(query);
        return RunCoreAsync(prompt, ct);
    }

    /// <inheritdoc/>
    public System.Threading.Tasks.Task<AgentResult> RunPromptAsync(
        string prompt, CancellationToken ct = default)
        => RunCoreAsync(prompt, ct);

    // ── Core subprocess execution ─────────────────────────────────────────────

    private async System.Threading.Tasks.Task<AgentResult> RunCoreAsync(string prompt, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var args = BuildArgs();
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = args,
            WorkingDirectory = _opts.ProjectDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        _logger.LogDebug("Starting Claude CLI. Args={Args} WorkingDir={Dir}", args, _opts.ProjectDirectory);

        using var process = new Process { StartInfo = psi };
        var lines = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lines.Add(e.Data);
            AppendLog(e.Data);
            ProcessStreamLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            _logger.LogDebug("[stderr] {Line}", e.Data);
            AppendLog("[ERR] " + e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.StandardInput.WriteAsync(prompt);
        process.StandardInput.Close();

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(_opts.Timeout);

        try
        {
            await process.WaitForExitAsync(linked.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            var reason = ct.IsCancellationRequested
                ? "cancelled by caller"
                : $"timeout after {_opts.Timeout.TotalMinutes:F0} min";
            _logger.LogWarning("Claude CLI terminated: {Reason}", reason);
            return AgentResult.Fail($"Operation terminated: {reason}.");
        }

        sw.Stop();
        _logger.LogDebug("Claude CLI exited. Code={Code} Elapsed={Ms}ms", process.ExitCode, sw.ElapsedMilliseconds);

        var resultLine = lines.LastOrDefault(l =>
            l.TrimStart().StartsWith("{") &&
            (l.Contains("\"type\":\"result\"") ||
             l.Contains("\"subtype\":\"success\"") ||
             l.Contains("\"subtype\":\"error_during_execution\"")));

        if (resultLine == null)
        {
            var tail = string.Join("\n", lines.TakeLast(5));
            _logger.LogError("No result line found in output. Last lines: {Lines}", tail);
            return AgentResult.Fail("No result received from CLI.");
        }

        return ParseResponse(resultLine, sw.Elapsed);
    }

    private string BuildArgs()
    {
        var sb = new StringBuilder();
        sb.Append($"/c \"{_opts.ClaudeExecutable}\"");
        sb.Append(" --output-format stream-json --verbose");
        sb.Append($" --max-turns {_opts.MaxTurns}");

        if (_opts.SkipPermissions)
            sb.Append(" --dangerously-skip-permissions");

        if (!string.IsNullOrWhiteSpace(_opts.McpConfigPath))
            sb.Append($" --mcp-config \"{_opts.McpConfigPath}\"");

        sb.Append(" --print");
        return sb.ToString();
    }

    // ── Stream processing ─────────────────────────────────────────────────────

    private void ProcessStreamLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line) || !line.TrimStart().StartsWith("{"))
            return;

        try
        {
            var ev = JsonSerializer.Deserialize<ClaudeStreamEvent>(line, _jsonOpts);
            if (ev == null) return;

            var msg = ev.type switch
            {
                "assistant" => ExtractTextOrTool(ev.message),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(msg))
                OnProgress?.Invoke(msg);
        }
        catch { /* malformed line — ignore */ }
    }

    private string? ExtractTextOrTool(ClaudeStreamMessage? message)
    {
        if (message?.content == null) return null;

        var parts = new List<string>();
        foreach (var c in message.content)
        {
            if (c.type == "text" && !string.IsNullOrWhiteSpace(c.text))
                parts.Add(c.text);
            else if (c.type == "tool_use" && c.name != null)
                parts.Add($"[tool] {ToolLabelFormatter(c.name)}");
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
    }

    // ── Response parsing ──────────────────────────────────────────────────────

    private AgentResult ParseResponse(string raw, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return AgentResult.Fail("No output received from CLI.");

        try
        {
            var json = JsonSerializer.Deserialize<ClaudeCliJson>(raw, _jsonOpts);

            if (json is null || json.type != "result")
            {
                _logger.LogDebug("Output was not structured JSON — returning as plain text.");
                return AgentResult.Ok(raw, null, duration);
            }

            if (json.subtype is "error_during_execution" || string.IsNullOrWhiteSpace(json.result))
                return AgentResult.Fail($"Agent reported an error: {json.result}");

            return AgentResult.Ok(json.result, json.session_id, duration);
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(ex, "Invalid JSON — returning as plain text.");
            return AgentResult.Ok(raw, null, duration);
        }
    }

    // ── Logging helper ────────────────────────────────────────────────────────

    private void AppendLog(string text)
    {
        if (string.IsNullOrEmpty(_opts.LogFilePath)) return;
        try { File.AppendAllText(_opts.LogFilePath, text + "\n"); } catch { }
    }

    // ── Default tool label map ────────────────────────────────────────────────

    /// <summary>
    /// Human-readable labels for standard Claude Code built-in tools.
    /// Returns the raw tool name for any unknown (MCP) tool.
    /// Replace <see cref="ToolLabelFormatter"/> to add your own MCP entries.
    /// </summary>
    public static string DefaultToolLabel(string toolName) => toolName switch
    {
        "Read"         => "Reading file...",
        "Write"        => "Writing file...",
        "Edit"         => "Editing file...",
        "Glob"         => "Searching files...",
        "Grep"         => "Searching content...",
        "Bash"         => "Running command...",
        "WebSearch"    => "Searching the web...",
        "WebFetch"     => "Fetching page...",
        "ToolSearch"   => "Looking up tools...",
        "Agent"        => "Spawning sub-agent...",
        "ListMcpResourcesTool" => "Listing MCP resources...",
        "ReadMcpResourceTool"  => "Reading MCP resource...",
        _              => toolName
    };
}
