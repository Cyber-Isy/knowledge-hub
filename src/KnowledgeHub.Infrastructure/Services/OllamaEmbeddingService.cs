using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OllamaEmbeddingService(
        HttpClient httpClient,
        string model,
        ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _model = model;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        try
        {
            var request = new OllamaEmbedRequest { Model = _model, Input = text };
            var response = await _httpClient.PostAsJsonAsync("api/embed", request, JsonOptions, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(JsonOptions, ct);
            if (result?.Embeddings is null || result.Embeddings.Count == 0)
                throw new InvalidOperationException("Ollama returned no embeddings.");

            return result.Embeddings[0];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding via Ollama for text of length {Length}", text.Length);
            throw;
        }
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
            return [];

        _logger.LogDebug("Generating embeddings for {Count} texts via Ollama", texts.Count);

        var embeddings = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            var embedding = await GenerateEmbeddingAsync(text, ct);
            embeddings.Add(embedding);
        }

        _logger.LogDebug("Successfully generated {Count} embeddings via Ollama", embeddings.Count);
        return embeddings;
    }

    private sealed class OllamaEmbedRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("input")]
        public string Input { get; set; } = string.Empty;
    }

    private sealed class OllamaEmbedResponse
    {
        [JsonPropertyName("embeddings")]
        public List<float[]> Embeddings { get; set; } = [];
    }
}
