namespace KnowledgeHub.API.Configuration;

public class BlobStorageSettings
{
    public const string SectionName = "AzureBlobStorage";

    public required string ConnectionString { get; set; }
    public string ContainerName { get; set; } = "documents";
}
