using KnowledgeHub.Core.Interfaces.Services;

namespace KnowledgeHub.Infrastructure.Services;

public class DocumentTextExtractorFactory
{
    private readonly IEnumerable<IDocumentTextExtractor> _extractors;

    public DocumentTextExtractorFactory(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _extractors = extractors;
    }

    public IDocumentTextExtractor GetExtractor(string contentType)
    {
        return _extractors.FirstOrDefault(e => e.CanHandle(contentType))
            ?? throw new NotSupportedException($"No text extractor available for content type '{contentType}'.");
    }
}
