using System.Text.RegularExpressions;

namespace AI_Dashboard.Application.Common.Helpers;

/// LLMs often wrap JSON in ```json fences, add commentary, or emit <think> blocks.
/// This strips all of that so System.Text.Json can parse the payload reliably.
public static class ResponseJsonParser
{
    private static readonly Regex FenceRegex = new(@"```(?:json)?\s*([\s\S]*?)```", RegexOptions.Compiled);
    private static readonly Regex ThinkRegex = new(@"<think>[\s\S]*?</think>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ExtractJson(string raw)
    {
        // Strip <think>...</think> blocks emitted by reasoning models (e.g. qwen3)
        raw = ThinkRegex.Replace(raw, string.Empty).Trim();

        var fenceMatch = FenceRegex.Match(raw);
        if (fenceMatch.Success)
            return fenceMatch.Groups[1].Value.Trim();

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw.Substring(start, end - start + 1);

        return raw.Trim();
    }
}