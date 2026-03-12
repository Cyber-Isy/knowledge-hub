using System.Collections.Concurrent;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Services;

/// <summary>
/// In-memory vector search using cosine similarity. For local development only.
/// Production uses Azure AI Search.
/// </summary>
public class InMemoryVectorSearchService : IVectorSearchService
{
    private readonly ConcurrentDictionary<Guid, VectorEntry> _vectors = new();
    private readonly ILogger<InMemoryVectorSearchService> _logger;

    public InMemoryVectorSearchService(ILogger<InMemoryVectorSearchService> logger)
    {
        _logger = logger;
    }

    public Task IndexChunkAsync(DocumentChunk chunk, float[] embedding, CancellationToken ct = default)
    {
        var entry = new VectorEntry(
            ChunkId: chunk.Id,
            DocumentId: chunk.DocumentId,
            Content: chunk.Content,
            FileName: chunk.Document?.FileName ?? string.Empty,
            Embedding: embedding);

        _vectors[chunk.Id] = entry;
        _logger.LogDebug("Indexed chunk {ChunkId} for document {DocumentId} (total: {Count})",
            chunk.Id, chunk.DocumentId, _vectors.Count);

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
    {
        if (_vectors.IsEmpty)
        {
            _logger.LogDebug("Vector store is empty, returning no results");
            return Task.FromResult<IReadOnlyList<VectorSearchResult>>([]);
        }

        var results = _vectors.Values
            .Select(entry => new
            {
                Entry = entry,
                Score = CosineSimilarity(queryEmbedding, entry.Embedding)
            })
            .OrderByDescending(x => x.Score)
            .Take(topK)
            .Where(x => x.Score > 0.1) // Filter out very low similarity
            .Select(x => new VectorSearchResult(
                DocumentChunkId: x.Entry.ChunkId,
                Content: x.Entry.Content,
                Score: x.Score,
                DocumentId: x.Entry.DocumentId,
                FileName: x.Entry.FileName))
            .ToList();

        _logger.LogDebug("Vector search returned {Count} results from {Total} indexed chunks",
            results.Count, _vectors.Count);

        return Task.FromResult<IReadOnlyList<VectorSearchResult>>(results);
    }

    public Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        var keysToRemove = _vectors
            .Where(kvp => kvp.Value.DocumentId == documentId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _vectors.TryRemove(key, out _);
        }

        _logger.LogInformation("Deleted {Count} chunks for document {DocumentId}", keysToRemove.Count, documentId);
        return Task.CompletedTask;
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * (double)b[i];
            magA += a[i] * (double)a[i];
            magB += b[i] * (double)b[i];
        }

        var magnitude = Math.Sqrt(magA) * Math.Sqrt(magB);
        return magnitude == 0 ? 0 : dot / magnitude;
    }

    private sealed record VectorEntry(
        Guid ChunkId,
        Guid DocumentId,
        string Content,
        string FileName,
        float[] Embedding);
}
