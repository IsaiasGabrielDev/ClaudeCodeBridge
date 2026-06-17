using System.Text.Json;

namespace ClaudeCodeBridge;

internal sealed record ClaudeStreamEvent
{
    public string type { get; init; } = "";
    public string? subtype { get; init; }
    public ClaudeStreamMessage? message { get; init; }
    public ClaudeStreamToolUse? tool_use { get; init; }
    public ClaudeStreamResult? result { get; init; }
}

internal sealed record ClaudeStreamMessage
{
    public string? role { get; init; }
    public List<ClaudeStreamContent>? content { get; init; }
}

internal sealed record ClaudeStreamContent
{
    public string type { get; init; } = "";
    public string? text { get; init; }
    public string? name { get; init; }
    public JsonElement? input { get; init; }
}

internal sealed record ClaudeStreamToolUse
{
    public string? name { get; init; }
    public JsonElement? input { get; init; }
}

internal sealed record ClaudeStreamResult
{
    public string? subtype { get; init; }
    public string? result { get; init; }
    public bool is_error { get; init; }
    public string? session_id { get; init; }
}
