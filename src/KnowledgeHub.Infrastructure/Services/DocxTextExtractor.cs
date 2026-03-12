using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using KnowledgeHub.Core.Interfaces.Services;

namespace KnowledgeHub.Infrastructure.Services;

public class DocxTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> SupportedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    public bool CanHandle(string contentType)
        => SupportedTypes.Contains(contentType);

    public Task<string> ExtractTextAsync(Stream fileStream, CancellationToken ct = default)
    {
        using var document = WordprocessingDocument.Open(fileStream, false);
        var body = document.MainDocumentPart?.Document.Body;

        if (body is null)
            return Task.FromResult(string.Empty);

        var sb = new StringBuilder();

        foreach (var paragraph in body.Elements<Paragraph>())
        {
            ct.ThrowIfCancellationRequested();

            var text = paragraph.InnerText;
            sb.AppendLine(text);
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }
}
