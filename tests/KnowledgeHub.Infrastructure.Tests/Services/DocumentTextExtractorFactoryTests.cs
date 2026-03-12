using FluentAssertions;
using KnowledgeHub.Core.Interfaces.Services;
using KnowledgeHub.Infrastructure.Services;
using NSubstitute;

namespace KnowledgeHub.Infrastructure.Tests.Services;

public class DocumentTextExtractorFactoryTests
{
    [Fact]
    public void GetExtractor_WithPdfContentType_ReturnsMatchingExtractor()
    {
        var pdfExtractor = Substitute.For<IDocumentTextExtractor>();
        pdfExtractor.CanHandle("application/pdf").Returns(true);

        var txtExtractor = Substitute.For<IDocumentTextExtractor>();
        txtExtractor.CanHandle("application/pdf").Returns(false);

        var factory = new DocumentTextExtractorFactory(new[] { pdfExtractor, txtExtractor });

        var result = factory.GetExtractor("application/pdf");

        result.Should().BeSameAs(pdfExtractor);
    }

    [Fact]
    public void GetExtractor_WithTextContentType_ReturnsMatchingExtractor()
    {
        var pdfExtractor = Substitute.For<IDocumentTextExtractor>();
        pdfExtractor.CanHandle("text/plain").Returns(false);

        var txtExtractor = Substitute.For<IDocumentTextExtractor>();
        txtExtractor.CanHandle("text/plain").Returns(true);

        var factory = new DocumentTextExtractorFactory(new[] { pdfExtractor, txtExtractor });

        var result = factory.GetExtractor("text/plain");

        result.Should().BeSameAs(txtExtractor);
    }

    [Fact]
    public void GetExtractor_WithDocxContentType_ReturnsMatchingExtractor()
    {
        var docxExtractor = Substitute.For<IDocumentTextExtractor>();
        docxExtractor.CanHandle("application/vnd.openxmlformats-officedocument.wordprocessingml.document").Returns(true);

        var factory = new DocumentTextExtractorFactory(new[] { docxExtractor });

        var result = factory.GetExtractor("application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        result.Should().BeSameAs(docxExtractor);
    }

    [Fact]
    public void GetExtractor_WithUnsupportedContentType_ThrowsNotSupportedException()
    {
        var pdfExtractor = Substitute.For<IDocumentTextExtractor>();
        pdfExtractor.CanHandle("image/png").Returns(false);

        var factory = new DocumentTextExtractorFactory(new[] { pdfExtractor });

        var act = () => factory.GetExtractor("image/png");

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*image/png*");
    }

    [Fact]
    public void GetExtractor_WithNoExtractors_ThrowsNotSupportedException()
    {
        var factory = new DocumentTextExtractorFactory(Array.Empty<IDocumentTextExtractor>());

        var act = () => factory.GetExtractor("application/pdf");

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void GetExtractor_WithMultipleMatching_ReturnsFirst()
    {
        var extractor1 = Substitute.For<IDocumentTextExtractor>();
        extractor1.CanHandle("text/plain").Returns(true);

        var extractor2 = Substitute.For<IDocumentTextExtractor>();
        extractor2.CanHandle("text/plain").Returns(true);

        var factory = new DocumentTextExtractorFactory(new[] { extractor1, extractor2 });

        var result = factory.GetExtractor("text/plain");

        result.Should().BeSameAs(extractor1);
    }
}
