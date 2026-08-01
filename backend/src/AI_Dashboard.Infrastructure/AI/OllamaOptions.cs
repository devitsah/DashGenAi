using AI_Dashboard.Application.Common.Interfaces;

namespace AI_Dashboard.Infrastructure.AI;

public class OllamaOptions : IOllamaModelOptions
{
    public string BaseUrl        { get; set; } = "http://10.254.252.55:11434";
    public string IntentModel    { get; set; } = "ministral-3:8b-instruct-2512-q8_0";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string CoderModel     { get; set; } = "ministral-3:8b-instruct-2512-q8_0";
    public int    TimeoutSeconds { get; set; } = 300;
    public bool   Think          { get; set; } = false;
    public double Temperature    { get; set; } = 0.1;
}
