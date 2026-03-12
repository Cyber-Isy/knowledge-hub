using System.Threading.Channels;
using KnowledgeHub.Core.Interfaces.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Infrastructure.Services;

public class DocumentProcessingBackgroundService : BackgroundService
{
    private readonly Channel<Guid> _channel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentProcessingBackgroundService> _logger;

    public DocumentProcessingBackgroundService(
        Channel<Guid> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<DocumentProcessingBackgroundService> logger)
    {
        _channel = channel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EnqueueAsync(Guid documentId, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(documentId, ct);
        _logger.LogInformation("Document {DocumentId} enqueued for processing", documentId);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document processing background service started");

        await foreach (var documentId in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("Processing document {DocumentId}", documentId);

                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
                await processingService.ProcessDocumentAsync(documentId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing document {DocumentId}", documentId);
            }
        }

        _logger.LogInformation("Document processing background service stopped");
    }
}
