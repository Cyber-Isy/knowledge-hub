using System.Text;
using KnowledgeHub.Core.Interfaces.Services;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace KnowledgeHub.Infrastructure.Services;

public class PdfTextExtractor : IDocumentTextExtractor
{
    public bool CanHandle(string contentType)
        => contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractTextAsync(Stream fileStream, CancellationToken ct = default)
    {
        using var document = PdfDocument.Open(fileStream);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            ct.ThrowIfCancellationRequested();

            var pageText = page.Text;
            if (!string.IsNullOrWhiteSpace(pageText))
            {
                sb.AppendLine(pageText);
                sb.AppendLine();
            }
        }

        return Task.FromResult(sb.ToString().TrimEnd());
    }
}
