using System.ClientModel;
using Azure;
using Azure.AI.OpenAI;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;
using OpenAI.Embeddings;

namespace KnowledgeHub.Infrastructure.Services;

public class AzureOpenAIEmbeddingService : IEmbeddingService
{
    private readonly EmbeddingClient _client;
    private readonly ILogger<AzureOpenAIEmbeddingService> _logger;

    public AzureOpenAIEmbeddingService(
        string endpoint,
        string apiKey,
        string deploymentName,
        ILogger<AzureOpenAIEmbeddingService> logger)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(endpoint),
            new AzureKeyCredential(apiKey));

        _client = azureClient.GetEmbeddingClient(deploymentName);
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        try
        {
            ClientResult<OpenAIEmbedding> result = await _client.GenerateEmbeddingAsync(text, cancellationToken: ct);
            return result.Value.ToFloats().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embedding for text of length {Length}", text.Length);
            throw;
        }
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        if (texts.Count == 0)
            return [];

        try
        {
            _logger.LogDebug("Generating embeddings for {Count} texts", texts.Count);

            ClientResult<OpenAIEmbeddingCollection> result =
                await _client.GenerateEmbeddingsAsync(texts, cancellationToken: ct);

            var embeddings = result.Value
                .OrderBy(e => e.Index)
                .Select(e => e.ToFloats().ToArray())
                .ToList();

            _logger.LogDebug("Successfully generated {Count} embeddings", embeddings.Count);
            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate embeddings for {Count} texts", texts.Count);
            throw;
        }
    }
}
