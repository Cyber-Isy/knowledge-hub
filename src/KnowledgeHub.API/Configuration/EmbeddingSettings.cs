namespace KnowledgeHub.API.Configuration;

public class EmbeddingSettings
{
    public const string SectionName = "AzureOpenAI";

    public required string Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public required string DeploymentName { get; set; }
}
