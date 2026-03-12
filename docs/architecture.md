# KnowledgeHub Architecture

## System Overview (C4 Context)

KnowledgeHub is a RAG (Retrieval-Augmented Generation) Chat Assistant that allows users to upload documents, process them into a searchable knowledge base, and interact with an AI that grounds its responses in the uploaded content.

**Actors:**
- **End User** -- Uploads documents, asks questions via chat, receives cited answers.
- **Admin** -- Manages users, views platform-wide statistics.

**External Systems:**
- **Azure OpenAI** -- GPT-4o for chat completions, text-embedding-3-small for vector embeddings.
- **Azure AI Search** -- Vector index for semantic search over document chunks.
- **Azure Blob Storage** (production) -- Stores uploaded document files.
- **Azure SQL** (production) / **SQLite** (development) -- Relational data store.

```
+----------+       +-------------------+       +------------------+
|          | HTTPS |                   | HTTPS |                  |
| End User +------>+ KnowledgeHub API  +------>+ Azure OpenAI     |
|          |       |  (.NET 10)        |       | (GPT-4o /        |
+----------+       |                   |       |  Embeddings)     |
                   +--------+----------+       +------------------+
+----------+       |        |
|          | HTTPS |        |  EF Core         +------------------+
|  Admin   +------>+        +----------------->+ SQLite / Azure   |
|          |       |        |                  | SQL Database     |
+----------+       |        |                  +------------------+
                   |        |
+----------+       |        |  Search SDK      +------------------+
| Angular  | SPA   |        +----------------->+ Azure AI Search  |
| Frontend +------>+        |                  | (Vector Index)   |
+----------+       |        |                  +------------------+
                   |        |
                   |        |  Blob SDK        +------------------+
                   |        +----------------->+ Azure Blob /     |
                   |                           | Local Disk       |
                   +---------------------------+------------------+
```

## Component Diagram

```
KnowledgeHub.sln
|
|-- KnowledgeHub.Core (Class Library, zero dependencies)
|   |-- Entities/         BaseEntity, Document, DocumentChunk, Conversation, Message, MessageSource
|   |-- Enums/            DocumentStatus, MessageRole
|   |-- Interfaces/       IRepository<T>, IDocumentRepository, IChatService, IFileStorageService,
|   |                     IDocumentTextExtractor, ITextChunker, IEmbeddingService, IVectorSearchService,
|   |                     IDocumentProcessingService, IDataSeeder
|   |-- Models/           PaginationParams, PagedResult<T>
|   +-- Configuration/    ChatSettings
|
|-- KnowledgeHub.Infrastructure (Class Library, depends on Core)
|   |-- Data/             ApplicationDbContext, ApplicationUser, DataSeeder, EntityTypeConfigurations
|   |-- Repositories/     Repository<T>, DocumentRepository
|   +-- Services/         AzureOpenAIEmbeddingService, AzureAISearchService,
|                         AzureBlobStorageService, LocalFileStorageService,
|                         PdfTextExtractor, DocxTextExtractor, PlainTextExtractor,
|                         RecursiveTextChunker, DocumentProcessingService,
|                         DocumentProcessingBackgroundService, ChatService
|
|-- KnowledgeHub.API (ASP.NET Core Web API, depends on Infrastructure + Core)
|   |-- Controllers/      AuthController, DocumentsController, ChatController,
|   |                     AdminController, HealthController
|   |-- Hubs/             ChatHub (SignalR)
|   |-- Middleware/        SecurityHeadersMiddleware, CorrelationIdMiddleware,
|   |                     RequestLoggingMiddleware, ExceptionHandlingMiddleware
|   |-- DTOs/             Request/Response models
|   |-- Validators/       FluentValidation validators, FileSignatureValidator
|   |-- Configuration/    JwtSettings, FileUploadSettings
|   +-- Extensions/       ServiceCollectionExtensions (DI registration)
|
+-- KnowledgeHub.Web (Angular 21 SPA)
    |-- core/             AuthService, AuthInterceptor, AuthGuard
    |-- features/
    |   |-- auth/         Login, Register components
    |   |-- documents/    Upload, List, Detail components
    |   +-- chat/         Chat interface, message components
    +-- shared/           UI components, pipes, directives
```

## RAG Pipeline Flow

```
1. UPLOAD        2. EXTRACT         3. CHUNK           4. EMBED            5. INDEX
+----------+    +--------------+   +--------------+   +---------------+   +---------------+
| User     |    | Text         |   | Recursive    |   | Azure OpenAI  |   | Azure AI      |
| uploads  +--->+ Extractor    +--->+ Text Chunker +--->+ Embedding    +--->+ Search        |
| PDF/DOCX |    | (PDF, DOCX,  |   | (512 tokens, |   | (text-embed-  |   | (vector       |
| TXT/MD   |    |  TXT)        |   |  50 overlap) |   |  ding-3-small)|   |  index)       |
+----------+    +--------------+   +--------------+   +---------------+   +---------------+
                                                                              |
6. QUERY         7. EMBED QUERY     8. SEARCH          9. BUILD PROMPT       |
+----------+    +---------------+   +---------------+   +---------------+     |
| User     |    | Azure OpenAI  |   | Azure AI      |   | System prompt |     |
| asks     +--->+ Embedding     +--->+ Search        +--->+ + context    |     |
| question |    | (same model)  |   | (top-k=5)     |   | chunks        |     |
+----------+    +---------------+   +-------+-------+   +-------+-------+     |
                                            |                    |             |
                                    10. GENERATE          11. RESPOND          |
                                    +---------------+   +---------------+      |
                                    | Azure OpenAI  |   | Response with |      |
                                    | GPT-4o        +--->+ source        |      |
                                    | (chat)        |   | citations     |      |
                                    +---------------+   +---------------+
```

**Processing details:**
- Document processing runs asynchronously via `DocumentProcessingBackgroundService` (IHostedService)
- Status transitions: Uploaded -> Processing -> Chunking -> Embedding -> Indexing -> Ready (or Failed)
- Real-time chat streaming uses SignalR (`ChatHub`) for token-by-token delivery
- Non-streaming chat uses the REST endpoint (`ChatController.SendMessage`)

## Database Schema

| Entity | Key Fields | Relationships |
|---|---|---|
| **ApplicationUser** (Identity) | Id (Guid), Email, DisplayName, CreatedAt | Has many Documents, Conversations |
| **Document** | Id, FileName, ContentType, FileSize, StoragePath, Status, UserId | Belongs to User; has many DocumentChunks |
| **DocumentChunk** | Id, Content, EmbeddingId, ChunkIndex, TokenCount, DocumentId | Belongs to Document; has many MessageSources |
| **Conversation** | Id, Title, UserId, IsArchived | Belongs to User; has many Messages |
| **Message** | Id, Content, Role (User/Assistant/System), TokensUsed, ConversationId | Belongs to Conversation; has many MessageSources |
| **MessageSource** | Id, RelevanceScore, MessageId, DocumentChunkId | Belongs to Message and DocumentChunk |

```
ApplicationUser 1---* Document 1---* DocumentChunk
                1---* Conversation 1---* Message 1---* MessageSource *---1 DocumentChunk
```

## API Endpoint Summary

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Register a new user | No |
| POST | `/api/v1/auth/login` | Authenticate and receive JWT | No |
| GET | `/api/v1/auth/me` | Get current user profile | Yes |
| POST | `/api/v1/documents/upload` | Upload a document | Yes |
| GET | `/api/v1/documents` | List user's documents (paginated) | Yes |
| GET | `/api/v1/documents/{id}` | Get document by ID | Yes |
| GET | `/api/v1/documents/{id}/download` | Download original file | Yes |
| DELETE | `/api/v1/documents/{id}` | Delete a document | Yes |
| GET | `/api/v1/documents/stats` | Get document statistics | Yes |
| POST | `/api/v1/chat/conversations` | Create a conversation | Yes |
| GET | `/api/v1/chat/conversations` | List conversations (paginated) | Yes |
| GET | `/api/v1/chat/conversations/{id}` | Get conversation by ID | Yes |
| GET | `/api/v1/chat/conversations/{id}/messages` | Get messages (paginated) | Yes |
| POST | `/api/v1/chat/conversations/{id}/messages` | Send message, get response | Yes |
| GET | `/api/v1/admin/stats` | Platform-wide statistics | Admin |
| GET | `/api/v1/admin/users` | List all users | Admin |
| PUT | `/api/v1/admin/users/{id}/toggle` | Enable/disable a user | Admin |
| GET | `/api/v1/health` | Health check | No |
| SignalR | `/hubs/chat` | Real-time chat streaming | Yes |

## Deployment Architecture

**Development:**
- Backend: `dotnet run` on https://localhost:5001
- Frontend: `ng serve` on http://localhost:4200
- Database: SQLite (local file)
- File storage: Local `uploads/` directory
- Docker Compose available for containerized local development

**Production (Azure):**
- Backend: Azure App Service (.NET 10)
- Frontend: Static web app or App Service
- Database: Azure SQL
- File storage: Azure Blob Storage
- Vector search: Azure AI Search
- AI: Azure OpenAI Service
- All secrets via environment variables or Azure Key Vault

**Infrastructure:**
```
                    +---------------------+
                    | Azure App Service   |
                    | (API + Angular SPA) |
                    +----------+----------+
                               |
          +--------------------+--------------------+
          |                    |                    |
+---------v------+  +----------v-------+  +---------v--------+
| Azure SQL      |  | Azure Blob       |  | Azure OpenAI     |
| (Relational)   |  | Storage (Files)  |  | (GPT-4o +        |
+----------------+  +------------------+  |  Embeddings)     |
                                          +---------+--------+
                                                    |
                                          +---------v--------+
                                          | Azure AI Search  |
                                          | (Vector Index)   |
                                          +------------------+
```
