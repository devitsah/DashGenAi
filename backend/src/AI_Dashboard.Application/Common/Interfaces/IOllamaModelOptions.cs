namespace AI_Dashboard.Application.Common.Interfaces;

public interface IOllamaModelOptions
{
    string IntentModel   { get; }
    string EmbeddingModel { get; }
    string CoderModel    { get; }
}
