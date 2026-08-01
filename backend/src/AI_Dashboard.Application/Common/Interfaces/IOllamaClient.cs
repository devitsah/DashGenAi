namespace AI_Dashboard.Application.Common.Interfaces;

public interface IOllamaClient
{
    /// Generate with the default intent model (qwen3:1.7b).
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
    Task<string> GenerateAsync(string systemPrompt, string userPrompt, int maxTokens, CancellationToken ct = default);

    /// Generate with a specific named model.
    Task<string> GenerateWithModelAsync(string model, string systemPrompt, string userPrompt, int maxTokens, CancellationToken ct = default);

    /// Embed text using the embedding model (nomic-embed-text).
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
