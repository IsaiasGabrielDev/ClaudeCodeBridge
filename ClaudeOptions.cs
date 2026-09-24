namespace ClaudeCodeBridge;

/// <summary>
/// Configuration for the Claude Code CLI integration.
/// Populate via appsettings.json, environment variables, or inline code.
/// </summary>
public sealed class ClaudeOptions
{
    /// <summary>
    /// Path to the Claude CLI executable.
    /// Default: "claude" (assumes it is on PATH).
    /// Windows example: <c>C:\Users\you\AppData\Roaming\npm\claude.cmd</c>
    /// </summary>
    public string ClaudeExecutable { get; set; } = "claude";

    /// <summary>
    /// Working directory for the CLI process.
    /// Should contain CLAUDE.md and the .claude/ folder (settings, skills, MCPs).
    /// </summary>
    public string ProjectDirectory { get; set; } = "";

    /// <summary>
    /// Optional path to a custom mcp.json configuration file.
    /// Leave null to use the CLI's default MCP configuration.
    /// </summary>
    public string? McpConfigPath { get; set; }

    /// <summary>
    /// Maximum number of agent turns per call.
    /// Increase for long-running autonomous tasks.
    /// </summary>
    public int MaxTurns { get; set; } = 30;

    /// <summary>Total timeout per CLI call.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(20);

    /// <summary>
    /// Passes <c>--dangerously-skip-permissions</c> to the CLI.
    /// Only enable in trusted, local-only environments.
    /// </summary>
    public bool SkipPermissions { get; set; } = false;

    /// <summary>
    /// Extra CLI arguments appended to every call (e.g. <c>--setting-sources project --no-session-persistence</c>).
    /// Use to isolate calls from user-level settings, skills or saved sessions.
    /// </summary>
    public string? ExtraArguments { get; set; }

    /// <summary>
    /// Optional file path for a verbose I/O log of all CLI communication.
    /// Leave null (default) to disable file logging.
    /// </summary>
    public string? LogFilePath { get; set; }
}
