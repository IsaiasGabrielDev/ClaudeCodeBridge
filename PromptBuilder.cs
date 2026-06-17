namespace ClaudeCodeBridge;

/// <summary>
/// Assembles prompts sent to the Claude agent.
/// All business rules must live in CLAUDE.md — this class only structures the input.
/// </summary>
internal static class PromptBuilder
{
    /// <summary>Prompt for a structured task with optional context data.</summary>
    internal static string Task(AgentContext ctx, string taskDescription)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"## Task: {ctx.Id}");

        if (!string.IsNullOrWhiteSpace(ctx.Group))
            sb.AppendLine($"- **Group:** {ctx.Group}");

        if (!string.IsNullOrWhiteSpace(ctx.ContentPath))
            sb.AppendLine($"- **Content path:** `{ctx.ContentPath}`");

        if (ctx.Metadata is { Count: > 0 })
        {
            foreach (var (key, value) in ctx.Metadata)
                sb.AppendLine($"- **{key}:** {value}");
        }

        sb.AppendLine();
        sb.AppendLine(taskDescription);

        if (!string.IsNullOrWhiteSpace(ctx.AdditionalInstruction))
        {
            sb.AppendLine();
            sb.AppendLine($"**Additional instruction:** {ctx.AdditionalInstruction}");
        }

        return sb.ToString();
    }

    /// <summary>Prompt for a free-form question, optionally scoped to a context.</summary>
    internal static string Query(AgentQuery query)
    {
        var ctx = query.Context;
        var sb = new System.Text.StringBuilder();

        if (ctx is not null)
        {
            sb.AppendLine($"## Query — {ctx.Id}");

            if (!string.IsNullOrWhiteSpace(ctx.Group))
                sb.AppendLine($"- **Group:** {ctx.Group}");

            if (!string.IsNullOrWhiteSpace(ctx.ContentPath))
                sb.AppendLine($"- **Content path:** `{ctx.ContentPath}`");

            if (ctx.Metadata is { Count: > 0 })
            {
                foreach (var (key, value) in ctx.Metadata)
                    sb.AppendLine($"- **{key}:** {value}");
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("## General query");
            sb.AppendLine();
        }

        sb.AppendLine($"**Question:** {query.Question}");
        sb.AppendLine();
        sb.AppendLine("Search the available data sources and files as needed to answer.");
        sb.AppendLine("If you cannot locate some data, report exactly what is missing.");

        return sb.ToString();
    }
}
