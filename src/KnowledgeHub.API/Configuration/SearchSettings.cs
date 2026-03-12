namespace KnowledgeHub.API.Configuration;

public class SearchSettings
{
    public const string SectionName = "AzureAISearch";

    public required string Endpoint { get; set; }
    public required string ApiKey { get; set; }
    public string IndexName { get; set; } = "knowledgehub-chunks";
}
