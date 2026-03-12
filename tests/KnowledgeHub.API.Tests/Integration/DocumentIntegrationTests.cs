using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using KnowledgeHub.Core.Models;

namespace KnowledgeHub.API.Tests.Integration;

[Collection("Integration")]
public class DocumentIntegrationTests
{
    private readonly CustomWebApplicationFactory _factory;

    public DocumentIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Upload_ThenGetAll_FileAppearsInList()
    {
        var (client, _, _) = await _factory.RegisterAndLoginAsync();

        // Upload a text file
        var fileContent = new StringContent("This is test content for integration testing.", Encoding.UTF8);
        var formContent = new MultipartFormDataContent();
        formContent.Add(fileContent, "file", "test-doc.txt");

        var uploadResponse = await client.PostAsync("/api/v1/documents/upload", formContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var uploadedDoc = await uploadResponse.Content.ReadFromJsonAsync<DocumentData>();
        uploadedDoc!.FileName.Should().Be("test-doc.txt");

        // Get all documents
        var listResponse = await client.GetAsync("/api/v1/documents");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var pagedResult = await listResponse.Content.ReadFromJsonAsync<PagedDocumentResult>();
        pagedResult!.Items.Should().Contain(d => d.FileName == "test-doc.txt");
    }

    [Fact]
    public async Task Upload_ThenDownload_ReturnsFileContent()
    {
        var (client, _, _) = await _factory.RegisterAndLoginAsync();
        var originalContent = "Download test content for verification.";

        var fileContent = new StringContent(originalContent, Encoding.UTF8);
        var formContent = new MultipartFormDataContent();
        formContent.Add(fileContent, "file", "download-test.txt");

        var uploadResponse = await client.PostAsync("/api/v1/documents/upload", formContent);
        uploadResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var uploadedDoc = await uploadResponse.Content.ReadFromJsonAsync<DocumentData>();

        // Download the file
        var downloadResponse = await client.GetAsync($"/api/v1/documents/{uploadedDoc!.Id}/download");
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var downloadedContent = await downloadResponse.Content.ReadAsStringAsync();
        downloadedContent.Should().Be(originalContent);
    }

    [Fact]
    public async Task Upload_ThenDelete_FileNoLongerInList()
    {
        var (client, _, _) = await _factory.RegisterAndLoginAsync();

        var fileContent = new StringContent("Content to be deleted.", Encoding.UTF8);
        var formContent = new MultipartFormDataContent();
        formContent.Add(fileContent, "file", "delete-test.txt");

        var uploadResponse = await client.PostAsync("/api/v1/documents/upload", formContent);
        var uploadedDoc = await uploadResponse.Content.ReadFromJsonAsync<DocumentData>();

        // Delete the document
        var deleteResponse = await client.DeleteAsync($"/api/v1/documents/{uploadedDoc!.Id}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify it no longer appears in list
        var listResponse = await client.GetAsync("/api/v1/documents");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<PagedDocumentResult>();
        pagedResult!.Items.Should().NotContain(d => d.Id == uploadedDoc.Id);
    }

    [Fact]
    public async Task Upload_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var fileContent = new StringContent("Unauthorized upload attempt.", Encoding.UTF8);
        var formContent = new MultipartFormDataContent();
        formContent.Add(fileContent, "file", "unauthorized.txt");

        var response = await client.PostAsync("/api/v1/documents/upload", formContent);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_OtherUsersDocument_ReturnsNotFound()
    {
        // User 1 uploads a document
        var (client1, _, _) = await _factory.RegisterAndLoginAsync();

        var fileContent = new StringContent("User 1's private document.", Encoding.UTF8);
        var formContent = new MultipartFormDataContent();
        formContent.Add(fileContent, "file", "private-doc.txt");

        var uploadResponse = await client1.PostAsync("/api/v1/documents/upload", formContent);
        var uploadedDoc = await uploadResponse.Content.ReadFromJsonAsync<DocumentData>();

        // User 2 tries to access it
        var (client2, _, _) = await _factory.RegisterAndLoginAsync();
        var getResponse = await client2.GetAsync($"/api/v1/documents/{uploadedDoc!.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private record DocumentData(Guid Id, string FileName, string ContentType, long FileSize, string Status,
        string? ErrorMessage, DateTime CreatedAt, DateTime UpdatedAt);
    private record PagedDocumentResult(List<DocumentData> Items, int TotalCount, int Page, int PageSize);
}
