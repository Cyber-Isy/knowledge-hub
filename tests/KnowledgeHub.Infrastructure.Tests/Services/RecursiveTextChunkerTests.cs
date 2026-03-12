using FluentAssertions;
using KnowledgeHub.Infrastructure.Services;

namespace KnowledgeHub.Infrastructure.Tests.Services;

public class RecursiveTextChunkerTests
{
    private readonly RecursiveTextChunker _chunker = new();

    [Fact]
    public void ChunkText_WithEmptyText_ReturnsEmptyList()
    {
        var result = _chunker.ChunkText("");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WithNullText_ReturnsEmptyList()
    {
        var result = _chunker.ChunkText(null!);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WithWhitespaceOnly_ReturnsEmptyList()
    {
        var result = _chunker.ChunkText("   \n\n   ");

        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_WithSingleParagraph_ReturnsSingleChunk()
    {
        var text = "This is a short paragraph that fits within a single chunk.";

        var result = _chunker.ChunkText(text, chunkSize: 512);

        result.Should().HaveCount(1);
        result[0].Content.Should().Be(text);
        result[0].ChunkIndex.Should().Be(0);
        result[0].TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ChunkText_WithMultipleParagraphs_CreatesMultipleChunks()
    {
        var paragraphs = Enumerable.Range(1, 20)
            .Select(i => $"This is paragraph number {i} with enough content to contribute to chunk splitting. " +
                         $"It contains several sentences that add to the overall token count. " +
                         $"Each paragraph has meaningful content for testing purposes.");
        var text = string.Join("\n\n", paragraphs);

        var result = _chunker.ChunkText(text, chunkSize: 50, overlap: 10);

        result.Should().HaveCountGreaterThan(1);

        for (int i = 0; i < result.Count; i++)
        {
            result[i].ChunkIndex.Should().Be(i);
            result[i].Content.Should().NotBeNullOrWhiteSpace();
            result[i].TokenCount.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public void ChunkText_WithOverlap_CreatesOverlappingChunks()
    {
        var paragraphs = Enumerable.Range(1, 10)
            .Select(i => $"Paragraph {i}: This is a detailed paragraph about topic {i} that contains enough words to require chunking.");
        var text = string.Join("\n\n", paragraphs);

        var result = _chunker.ChunkText(text, chunkSize: 30, overlap: 10);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_WithZeroOverlap_DoesNotOverlap()
    {
        var paragraphs = Enumerable.Range(1, 10)
            .Select(i => $"Paragraph {i}: Content about topic {i} with enough words for multiple chunks.");
        var text = string.Join("\n\n", paragraphs);

        var result = _chunker.ChunkText(text, chunkSize: 30, overlap: 0);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_WithVerySmallChunkSize_StillProducesChunks()
    {
        var text = "First paragraph with some content.\n\nSecond paragraph with more content.";

        var result = _chunker.ChunkText(text, chunkSize: 5, overlap: 0);

        result.Should().HaveCountGreaterThanOrEqualTo(2);
        result.Should().AllSatisfy(chunk =>
        {
            chunk.Content.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void ChunkText_PreservesAllContent()
    {
        var text = "First paragraph.\n\nSecond paragraph.\n\nThird paragraph.";

        var result = _chunker.ChunkText(text, chunkSize: 512, overlap: 0);

        var allContent = string.Join(" ", result.Select(c => c.Content));
        allContent.Should().Contain("First paragraph");
        allContent.Should().Contain("Second paragraph");
        allContent.Should().Contain("Third paragraph");
    }

    [Fact]
    public void ChunkText_WithLargeParagraph_SplitsBySentences()
    {
        var sentences = Enumerable.Range(1, 50)
            .Select(i => $"This is sentence number {i} in a very large paragraph.");
        var text = string.Join(" ", sentences);

        var result = _chunker.ChunkText(text, chunkSize: 30, overlap: 5);

        result.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void ChunkText_ChunkIndicesAreSequential()
    {
        var paragraphs = Enumerable.Range(1, 15)
            .Select(i => $"Paragraph {i} with content for testing chunk index ordering and sequencing.");
        var text = string.Join("\n\n", paragraphs);

        var result = _chunker.ChunkText(text, chunkSize: 30, overlap: 5);

        for (int i = 0; i < result.Count; i++)
        {
            result[i].ChunkIndex.Should().Be(i);
        }
    }
}
