namespace AI_Dashboard.Application.PromptProcessing.Models;

public class ClarificationRequest
{
    public string Question { get; set; } = default!;
    public List<string>? Options { get; set; }
}