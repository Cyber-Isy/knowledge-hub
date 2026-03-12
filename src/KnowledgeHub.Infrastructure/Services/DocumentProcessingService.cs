using KnowledgeHub.Core.Entities;
using KnowledgeHub.Core.Enums;
using KnowledgeHub.Core.Interfaces.Repositories;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Services;

public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IFileStorageService _fileStorageService;
    private readonly DocumentTextExtractorFactory _extractorFactory;
    private readonly ITextChunker _textChunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorSearchService _vectorSearchService;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        IDocumentRepository documentRepository,
        IFileStorageService fileStorageService,
        DocumentTextExtractorFactory extractorFactory,
        ITextChunker textChunker,
        IEmbeddingService embeddingService,
        IVectorSearchService vectorSearchService,
        ILogger<DocumentProcessingService> logger)
    {
        _documentRepository = documentRepository;
        _fileStorageService = fileStorageService;
        _extractorFactory = extractorFactory;
        _textChunker = textChunker;
        _embeddingService = embeddingService;
        _vectorSearchService = vectorSearchService;
        _logger = logger;
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await _documentRepository.GetByIdAsync(documentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found, skipping processing", documentId);
            return;
        }

        try
        {
            // Stage 1: Extract text
            await UpdateStatusAsync(document, DocumentStatus.Processing, ct);
            var extractor = _extractorFactory.GetExtractor(document.ContentType);
            await using var fileStream = await _fileStorageService.GetFileAsync(document.StoragePath!, ct);
            var text = await extractor.ExtractTextAsync(fileStream, ct);

            if (string.IsNullOrWhiteSpace(text))
            {
                await FailDocumentAsync(document, "No text could be extracted from the document.", ct);
                return;
            }

            _logger.LogInformation("Extracted {Length} characters from document {DocumentId}", text.Length, documentId);

            // Stage 2: Chunk text
            await UpdateStatusAsync(document, DocumentStatus.Chunking, ct);
            var textChunks = _textChunker.ChunkText(text);

            if (textChunks.Count == 0)
            {
                await FailDocumentAsync(document, "No chunks were produced from the extracted text.", ct);
                return;
            }

            _logger.LogInformation("Created {Count} chunks for document {DocumentId}", textChunks.Count, documentId);

            // Create DocumentChunk entities
            var chunks = textChunks.Select(tc => new DocumentChunk
            {
                Content = tc.Content,
                ChunkIndex = tc.ChunkIndex,
                TokenCount = tc.TokenCount,
                DocumentId = documentId,
                Document = document
            }).ToList();

            // Stage 3: Generate embeddings
            await UpdateStatusAsync(document, DocumentStatus.Embedding, ct);
            var chunkTexts = chunks.Select(c => c.Content).ToList();
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunkTexts, ct);

            _logger.LogInformation("Generated {Count} embeddings for document {DocumentId}", embeddings.Count, documentId);

            // Stage 4: Index in vector store
            await UpdateStatusAsync(document, DocumentStatus.Indexing, ct);
            for (var i = 0; i < chunks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                await _vectorSearchService.IndexChunkAsync(chunks[i], embeddings[i], ct);
                chunks[i].EmbeddingId = chunks[i].Id.ToString();
            }

            // Save chunks to database
            document.Chunks = chunks;
            await UpdateStatusAsync(document, DocumentStatus.Ready, ct);

            _logger.LogInformation("Document {DocumentId} processing completed successfully", documentId);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Document {DocumentId} processing was cancelled", documentId);
            await FailDocumentAsync(document, "Processing was cancelled.", CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Document {DocumentId} processing failed", documentId);
            await FailDocumentAsync(document, $"Processing failed: {ex.Message}", CancellationToken.None);
        }
    }

    private async Task UpdateStatusAsync(Document document, DocumentStatus status, CancellationToken ct)
    {
        document.Status = status;
        document.ErrorMessage = null;
        await _documentRepository.UpdateAsync(document, ct);
    }

    private async Task FailDocumentAsync(Document document, string error, CancellationToken ct)
    {
        document.Status = DocumentStatus.Failed;
        document.ErrorMessage = error;
        await _documentRepository.UpdateAsync(document, ct);
    }
}
