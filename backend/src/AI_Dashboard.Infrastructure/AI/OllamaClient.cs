using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AI_Dashboard.Application.Common.Interfaces;
using Microsoft.Extensions.Options;

namespace AI_Dashboard.Infrastructure.AI;

public class OllamaClient : IOllamaClient
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _opts;
    private static readonly Regex ThinkRegex = new(@"<think>[\s\S]*?</think>", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public OllamaClient(HttpClient http, IOptions<OllamaOptions> options)
    {
        _http = http;
        _opts = options.Value;
        _http.BaseAddress = new Uri(_opts.BaseUrl);
    }

    public Task<string> GenerateAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
        => GenerateWithModelAsync(_opts.IntentModel, systemPrompt, userPrompt, 256, ct);

    public Task<string> GenerateAsync(string systemPrompt, string userPrompt, int maxTokens, CancellationToken ct = default)
        => GenerateWithModelAsync(_opts.IntentModel, systemPrompt, userPrompt, maxTokens, ct);

    public async Task<string> GenerateWithModelAsync(string model, string systemPrompt, string userPrompt, int maxTokens, CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            prompt = $"{systemPrompt}\n\n{userPrompt}",
            stream = false,
            think = _opts.Think,
            options = new { temperature = _opts.Temperature, num_predict = maxTokens }
        };

        var response = await _http.PostAsync("/api/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var raw = doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        // Strip <think> blocks — some models emit them regardless of the think flag
        return ThinkRegex.Replace(raw, string.Empty).Trim();
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var payload = new { model = _opts.EmbeddingModel, input = text };

        var response = await _http.PostAsync("/api/embed",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        // /api/embed returns { "embeddings": [[...]] }
        var embeddings = doc.RootElement.GetProperty("embeddings")[0];
        return embeddings.EnumerateArray().Select(e => e.GetSingle()).ToArray();
    }
}
