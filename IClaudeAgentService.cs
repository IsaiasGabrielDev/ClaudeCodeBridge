namespace ClaudeCodeBridge;

/// <summary>
/// Abstraction over the Claude Code CLI subprocess.
/// Consume this interface to keep your application code decoupled from the implementation
/// and to allow mocking in unit tests.
/// </summary>
public interface IClaudeAgentService
{
    /// <summary>
    /// Raised on a background thread each time the agent emits text or invokes a tool.
    /// Use <c>Invoke</c> / <c>BeginInvoke</c> when updating UI from this event.
    /// </summary>
    event Action<string>? OnProgress;

    /// <summary>
    /// Sends a structured task to the agent together with context data.
    /// The prompt is assembled by <see cref="PromptBuilder"/>; all domain rules
    /// should live in CLAUDE.md inside <see cref="ClaudeOptions.ProjectDirectory"/>.
    /// </summary>
    Task<AgentResult> ExecuteAsync(AgentContext context, string task, CancellationToken ct = default);

    /// <summary>
    /// Sends a free-form question to the agent, optionally scoped to a context.
    /// </summary>
    Task<AgentResult> QueryAsync(AgentQuery query, CancellationToken ct = default);

    /// <summary>
    /// Sends a fully-formed prompt string directly to the CLI.
    /// Use when you need complete control over prompt construction.
    /// </summary>
    Task<AgentResult> RunPromptAsync(string prompt, CancellationToken ct = default);
}
