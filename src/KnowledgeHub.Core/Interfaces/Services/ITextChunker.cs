using KnowledgeHub.Core.Models;

namespace KnowledgeHub.Core.Interfaces.Services;

public interface ITextChunker
{
    IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 50);
}
