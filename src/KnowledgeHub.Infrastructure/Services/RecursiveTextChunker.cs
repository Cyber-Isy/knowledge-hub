using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Core.Models;

namespace KnowledgeHub.Infrastructure.Services;

public class RecursiveTextChunker : ITextChunker
{
    private static readonly string[] ParagraphSeparators = ["\r\n\r\n", "\n\n"];
    private static readonly string[] SentenceEndings = [".", "!", "?"];

    public IReadOnlyList<TextChunk> ChunkText(string text, int chunkSize = 512, int overlap = 50)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var paragraphs = SplitByParagraphs(text);
        var chunks = new List<TextChunk>();
        var currentChunk = new List<string>();
        var currentTokens = 0;
        var chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            var paragraphTokens = EstimateTokenCount(paragraph);

            if (paragraphTokens > chunkSize)
            {
                // Flush current chunk before processing large paragraph
                if (currentChunk.Count > 0)
                {
                    chunks.Add(CreateChunk(currentChunk, chunkIndex++));
                    currentChunk = GetOverlapContent(currentChunk, overlap);
                    currentTokens = EstimateTokenCount(string.Join(" ", currentChunk));
                }

                // Split large paragraph by sentences
                var sentenceChunks = SplitBySentences(paragraph, chunkSize, overlap);
                foreach (var sentenceChunk in sentenceChunks)
                {
                    chunks.Add(new TextChunk(sentenceChunk, chunkIndex++, EstimateTokenCount(sentenceChunk)));
                }

                currentChunk = [];
                currentTokens = 0;
                continue;
            }

            if (currentTokens + paragraphTokens > chunkSize && currentChunk.Count > 0)
            {
                chunks.Add(CreateChunk(currentChunk, chunkIndex++));
                currentChunk = GetOverlapContent(currentChunk, overlap);
                currentTokens = EstimateTokenCount(string.Join(" ", currentChunk));
            }

            currentChunk.Add(paragraph);
            currentTokens += paragraphTokens;
        }

        if (currentChunk.Count > 0)
        {
            chunks.Add(CreateChunk(currentChunk, chunkIndex));
        }

        return chunks;
    }

    private static List<string> SplitByParagraphs(string text)
    {
        var parts = text.Split(ParagraphSeparators, StringSplitOptions.RemoveEmptyEntries);
        return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
    }

    private static List<string> SplitBySentences(string text, int chunkSize, int overlap)
    {
        var sentences = new List<string>();
        var remaining = text;

        while (remaining.Length > 0)
        {
            var bestSplit = -1;
            foreach (var ending in SentenceEndings)
            {
                var idx = remaining.IndexOf(ending, StringComparison.Ordinal);
                if (idx >= 0 && (bestSplit < 0 || idx < bestSplit))
                    bestSplit = idx + ending.Length;
            }

            if (bestSplit < 0)
            {
                sentences.Add(remaining.Trim());
                break;
            }

            sentences.Add(remaining[..bestSplit].Trim());
            remaining = remaining[bestSplit..].TrimStart();
        }

        var chunks = new List<string>();
        var current = new List<string>();
        var currentTokens = 0;

        foreach (var sentence in sentences)
        {
            var sentenceTokens = EstimateTokenCount(sentence);

            if (currentTokens + sentenceTokens > chunkSize && current.Count > 0)
            {
                chunks.Add(string.Join(" ", current));
                current = GetOverlapContent(current, overlap);
                currentTokens = EstimateTokenCount(string.Join(" ", current));
            }

            current.Add(sentence);
            currentTokens += sentenceTokens;
        }

        if (current.Count > 0)
        {
            chunks.Add(string.Join(" ", current));
        }

        return chunks;
    }

    private static TextChunk CreateChunk(List<string> parts, int index)
    {
        var content = string.Join("\n\n", parts);
        return new TextChunk(content, index, EstimateTokenCount(content));
    }

    private static List<string> GetOverlapContent(List<string> parts, int overlapTokens)
    {
        if (overlapTokens <= 0)
            return [];

        var result = new List<string>();
        var tokens = 0;

        for (var i = parts.Count - 1; i >= 0; i--)
        {
            var partTokens = EstimateTokenCount(parts[i]);
            if (tokens + partTokens > overlapTokens && result.Count > 0)
                break;

            result.Insert(0, parts[i]);
            tokens += partTokens;
        }

        return result;
    }

    private static int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var wordCount = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        return (int)Math.Ceiling(wordCount * 1.3);
    }
}
