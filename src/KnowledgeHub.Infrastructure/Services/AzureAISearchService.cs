using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Services;

public class AzureAISearchService : IVectorSearchService
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly string _indexName;
    private readonly ILogger<AzureAISearchService> _logger;
    private bool _indexEnsured;

    private const int EmbeddingDimensions = 1536; // text-embedding-3-small

    public AzureAISearchService(
        string endpoint,
        string apiKey,
        string indexName,
        ILogger<AzureAISearchService> logger)
    {
        var credential = new AzureKeyCredential(apiKey);
        var serviceEndpoint = new Uri(endpoint);

        _indexClient = new SearchIndexClient(serviceEndpoint, credential);
        _searchClient = new SearchClient(serviceEndpoint, indexName, credential);
        _indexName = indexName;
        _logger = logger;
    }

    public async Task IndexChunkAsync(DocumentChunk chunk, float[] embedding, CancellationToken ct = default)
    {
        await EnsureIndexExistsAsync(ct);

        var document = new SearchDocument
        {
            ["id"] = chunk.Id.ToString(),
            ["documentId"] = chunk.DocumentId.ToString(),
            ["content"] = chunk.Content,
            ["chunkIndex"] = chunk.ChunkIndex,
            ["fileName"] = chunk.Document?.FileName ?? string.Empty,
            ["embedding"] = embedding
        };

        try
        {
            await _searchClient.MergeOrUploadDocumentsAsync(new[] { document }, cancellationToken: ct);
            _logger.LogDebug("Indexed chunk {ChunkId} for document {DocumentId}", chunk.Id, chunk.DocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index chunk {ChunkId}", chunk.Id);
            throw;
        }
    }

    public async Task<IReadOnlyList<VectorSearchResult>> SearchAsync(
        float[] queryEmbedding, int topK = 5, CancellationToken ct = default)
    {
        await EnsureIndexExistsAsync(ct);

        var vectorQuery = new VectorizedQuery(queryEmbedding)
        {
            KNearestNeighborsCount = topK,
            Fields = { "embedding" }
        };

        var searchOptions = new SearchOptions
        {
            VectorSearch = new()
            {
                Queries = { vectorQuery }
            },
            Size = topK,
            Select = { "id", "documentId", "content", "fileName" }
        };

        try
        {
            var response = await _searchClient.SearchAsync<SearchDocument>(searchOptions, ct);
            var results = new List<VectorSearchResult>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                var doc = result.Document;
                results.Add(new VectorSearchResult(
                    DocumentChunkId: Guid.Parse(doc["id"].ToString()!),
                    Content: doc["content"].ToString()!,
                    Score: result.Score ?? 0,
                    DocumentId: Guid.Parse(doc["documentId"].ToString()!),
                    FileName: doc["fileName"].ToString()!));
            }

            _logger.LogDebug("Vector search returned {Count} results", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector search failed");
            throw;
        }
    }

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        await EnsureIndexExistsAsync(ct);

        try
        {
            var searchOptions = new SearchOptions
            {
                Filter = $"documentId eq '{documentId}'",
                Select = { "id" },
                Size = 1000
            };

            var response = await _searchClient.SearchAsync<SearchDocument>(searchOptions, ct);
            var idsToDelete = new List<string>();

            await foreach (var result in response.Value.GetResultsAsync())
            {
                idsToDelete.Add(result.Document["id"].ToString()!);
            }

            if (idsToDelete.Count > 0)
            {
                var batch = IndexDocumentsBatch.Delete("id", idsToDelete);
                await _searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
                _logger.LogInformation("Deleted {Count} chunks for document {DocumentId}", idsToDelete.Count, documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete chunks for document {DocumentId}", documentId);
            throw;
        }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken ct)
    {
        if (_indexEnsured)
            return;

        try
        {
            await _indexClient.GetIndexAsync(_indexName, ct);
            _indexEnsured = true;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogInformation("Creating search index '{IndexName}'", _indexName);
            await CreateIndexAsync(ct);
            _indexEnsured = true;
        }
    }

    private async Task CreateIndexAsync(CancellationToken ct)
    {
        var index = new SearchIndex(_indexName)
        {
            Fields =
            {
                new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                new SearchableField("content") { AnalyzerName = LexicalAnalyzerName.StandardLucene },
                new SimpleField("chunkIndex", SearchFieldDataType.Int32) { IsSortable = true },
                new SimpleField("fileName", SearchFieldDataType.String) { IsFilterable = true },
                new SearchField("embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                {
                    IsSearchable = true,
                    VectorSearchDimensions = EmbeddingDimensions,
                    VectorSearchProfileName = "default-profile"
                }
            },
            VectorSearch = new VectorSearch
            {
                Profiles =
                {
                    new VectorSearchProfile("default-profile", "default-hnsw")
                },
                Algorithms =
                {
                    new HnswAlgorithmConfiguration("default-hnsw")
                    {
                        Parameters = new HnswParameters
                        {
                            Metric = VectorSearchAlgorithmMetric.Cosine,
                            M = 4,
                            EfConstruction = 400,
                            EfSearch = 500
                        }
                    }
                }
            }
        };

        await _indexClient.CreateIndexAsync(index, ct);
        _logger.LogInformation("Search index '{IndexName}' created", _indexName);
    }
}
