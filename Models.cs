namespace ClaudeCodeBridge;

// ── INPUT ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Generic context handed to the agent before it starts a task.
/// All fields are optional except <see cref="Id"/>.
/// The consuming application is responsible for building the context
/// that makes sense for its domain.
/// </summary>
public sealed class AgentContext
{
    /// <summary>Primary identifier (record ID, case number, order ID, etc.).</summary>
    public required string Id { get; init; }

    /// <summary>Optional grouping label (customer, department, project, etc.).</summary>
    public string? Group { get; init; }

    /// <summary>Optional filesystem path to content files related to this context.</summary>
    public string? ContentPath { get; init; }

    /// <summary>
    /// Free-form instruction appended to the task prompt.
    /// Use to focus or restrict the agent at runtime without changing CLAUDE.md.
    /// </summary>
    public string? AdditionalInstruction { get; init; }

    /// <summary>Arbitrary key-value pairs the agent can reference in its prompt.</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}

/// <summary>
/// A free-form question directed at the agent, optionally scoped to a context.
/// </summary>
public sealed class AgentQuery
{
    /// <summary>Current context (optional — omit for general questions).</summary>
    public AgentContext? Context { get; init; }

    /// <summary>Natural language question for the agent.</summary>
    public required string Question { get; init; }
}

// ── OUTPUT ────────────────────────────────────────────────────────────────────

/// <summary>Result returned by any agent call.</summary>
public sealed class AgentResult
{
    /// <summary>Indicates whether the agent completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>Agent response in markdown — answer, report, or descriptive error.</summary>
    public string Content { get; init; } = "";

    /// <summary>Error description when <see cref="Success"/> is <c>false</c>.</summary>
    public string? Error { get; init; }

    /// <summary>Claude session identifier returned by the CLI.</summary>
    public string? SessionId { get; init; }

    /// <summary>Total wall-clock time for the agent call.</summary>
    public TimeSpan Duration { get; init; }

    internal static AgentResult Ok(string content, string? sessionId, TimeSpan duration) =>
        new() { Success = true, Content = content, SessionId = sessionId, Duration = duration };

    internal static AgentResult Fail(string error) =>
        new() { Success = false, Error = error };
}

// ── INTERNAL: CLI JSON response DTO ───────────────────────────────────────────

internal sealed record ClaudeCliJson(
    string type,
    string subtype,
    string result,
    string session_id,
    decimal total_cost_usd,
    int duration_ms,
    int num_turns
);
