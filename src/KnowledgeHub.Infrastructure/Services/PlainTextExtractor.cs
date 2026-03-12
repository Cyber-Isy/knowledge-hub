using KnowledgeHub.Core.Interfaces.Services;

namespace KnowledgeHub.Infrastructure.Services;

public class PlainTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/markdown"
    };

    public bool CanHandle(string contentType)
        => SupportedTypes.Contains(contentType);

    public async Task<string> ExtractTextAsync(Stream fileStream, CancellationToken ct = default)
    {
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var text = await reader.ReadToEndAsync(ct);
        return text.TrimEnd();
    }
}
