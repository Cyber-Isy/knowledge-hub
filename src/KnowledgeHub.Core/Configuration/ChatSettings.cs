namespace KnowledgeHub.Core.Configuration;

public class ChatSettings
{
    public const string SectionName = "Chat";

    public string SystemPrompt { get; set; } =
        "You are a helpful assistant. Answer questions based on the provided context. " +
        "If the context does not contain relevant information, say so clearly. " +
        "Always cite your sources by referencing the document name.";

    public int MaxContextChunks { get; set; } = 5;
    public string ModelDeploymentName { get; set; } = "gpt-4o";
}
