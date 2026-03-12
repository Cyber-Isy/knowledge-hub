namespace KnowledgeHub.API.Configuration;

public static class FileUploadSettings
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB
    public static readonly string[] AllowedExtensions = [".pdf", ".docx", ".txt", ".md"];
    public static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown"
    ];
}
