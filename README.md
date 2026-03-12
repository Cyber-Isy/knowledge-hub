# KnowledgeHub

AI-powered RAG (Retrieval-Augmented Generation) Chat Assistant with Knowledge Base management.

Upload documents, process them into searchable knowledge, and chat with an AI that provides answers grounded in your documents — with source citations.

## Features

- **Document Management** — Upload PDF, DOCX, TXT, and Markdown files with file type validation (magic bytes)
- **RAG Pipeline** — Automatic text extraction, recursive chunking, embedding generation, and vector indexing
- **AI Chat** — Ask questions and get answers grounded in your uploaded documents
- **Source Citations** — Every response includes references to the source document chunks
- **Real-time Streaming** — Token-by-token response streaming via SignalR
- **Admin Dashboard** — User management (enable/disable), platform-wide statistics
- **Authentication** — JWT-based auth with ASP.NET Core Identity, role-based access control
- **Rate Limiting** — Per-endpoint rate limiting (auth, upload, chat) to prevent abuse
- **API Versioning** — URL-based versioning (v1) with Swagger/OpenAPI documentation
- **Security Headers** — CSP, X-Frame-Options, X-Content-Type-Options, Referrer-Policy, Permissions-Policy
- **Background Processing** — Documents are processed asynchronously via a hosted background service
- **Request Logging** — Structured logging with Serilog, correlation IDs for request tracing
- **Multi-language** — DE, EN, FR, IT
- **Dark/Light Theme** — User-selectable with system preference detection

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 10 |
| Frontend | Angular 21 (standalone components, signals) |
| Styling | Tailwind CSS |
| Database | SQLite (dev) / Azure SQL (prod) |
| Vector Store | Azure AI Search |
| AI Models | Azure OpenAI (GPT-4o + text-embedding-3-small) |
| File Storage | Local disk (dev) / Azure Blob Storage (prod) |
| Real-time | SignalR |
| Auth | ASP.NET Core Identity + JWT Bearer |
| Validation | FluentValidation |
| Logging | Serilog (console + file sinks) |
| API Docs | Swashbuckle (Swagger/OpenAPI) with XML comments |
| Testing | xUnit + Moq |
| Containerization | Docker + Docker Compose |
| CI/CD | GitHub Actions |
| Deployment | Azure App Service |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Angular CLI 21](https://angular.dev/)
- (Optional) [Docker](https://www.docker.com/) for containerized development

## Getting Started

### Backend

```bash
cd src/KnowledgeHub.API
dotnet restore
dotnet run
```

The API runs at `https://localhost:5001` with Swagger UI at `/swagger`.

### Frontend

```bash
cd src/KnowledgeHub.Web
npm install
ng serve
```

The app runs at `http://localhost:4200`.

### Docker (full stack)

```bash
docker compose up
```

This starts the API on port 5001 and the web app on port 4200.

### Database Migrations

```bash
cd src/KnowledgeHub.API
dotnet ef migrations add <MigrationName> --project ../KnowledgeHub.Infrastructure
dotnet ef database update --project ../KnowledgeHub.Infrastructure
```

In development, migrations are applied automatically on startup.

### Seed Data

In development mode, demo data is seeded automatically:

| Account | Email | Password | Role |
|---|---|---|---|
| Demo User | demo@knowledgehub.ch | Demo1234! | User |
| Admin | admin@knowledgehub.ch | Admin1234! | Admin |

## Environment Variables

| Variable | Description | Required | Default |
|---|---|---|---|
| `ConnectionStrings__DefaultConnection` | Database connection string | Yes | `Data Source=knowledgehub.db` (dev) |
| `Jwt__Secret` | JWT signing key (min. 32 chars) | Yes | Set in `appsettings.Development.json` for dev |
| `Jwt__Issuer` | JWT token issuer | No | `KnowledgeHub` |
| `Jwt__Audience` | JWT token audience | No | `KnowledgeHub` |
| `Jwt__ExpirationInMinutes` | Token lifetime in minutes | No | `60` |
| `AzureOpenAI__Endpoint` | Azure OpenAI resource endpoint | Yes (for AI features) | — |
| `AzureOpenAI__ApiKey` | Azure OpenAI API key | Yes (for AI features) | — |
| `AzureOpenAI__EmbeddingDeploymentName` | Embedding model deployment | No | `text-embedding-3-small` |
| `AzureOpenAI__ChatDeploymentName` | Chat model deployment | No | `gpt-4o` |
| `AzureAISearch__Endpoint` | Azure AI Search endpoint | Yes (for AI features) | — |
| `AzureAISearch__ApiKey` | Azure AI Search admin key | Yes (for AI features) | — |
| `AzureAISearch__IndexName` | Search index name | No | `knowledgehub-chunks` |
| `AzureBlobStorage__ConnectionString` | Blob storage connection (prod) | Prod only | — |
| `AzureBlobStorage__ContainerName` | Blob container name | No | `documents` |
| `Cors__AllowedOrigins__0` | Allowed CORS origin | No | `http://localhost:4200` |

## API Endpoints

| Method | Route | Description | Auth |
|---|---|---|---|
| POST | `/api/v1/auth/register` | Register a new user | No |
| POST | `/api/v1/auth/login` | Authenticate and receive JWT | No |
| GET | `/api/v1/auth/me` | Get current user profile | Yes |
| POST | `/api/v1/documents/upload` | Upload a document | Yes |
| GET | `/api/v1/documents` | List documents (paginated) | Yes |
| GET | `/api/v1/documents/{id}` | Get document by ID | Yes |
| GET | `/api/v1/documents/{id}/download` | Download original file | Yes |
| DELETE | `/api/v1/documents/{id}` | Delete a document | Yes |
| GET | `/api/v1/documents/stats` | Document statistics | Yes |
| POST | `/api/v1/chat/conversations` | Create a conversation | Yes |
| GET | `/api/v1/chat/conversations` | List conversations (paginated) | Yes |
| GET | `/api/v1/chat/conversations/{id}` | Get conversation by ID | Yes |
| GET | `/api/v1/chat/conversations/{id}/messages` | Get messages (paginated) | Yes |
| POST | `/api/v1/chat/conversations/{id}/messages` | Send message, get response | Yes |
| GET | `/api/v1/admin/stats` | Platform statistics | Admin |
| GET | `/api/v1/admin/users` | List all users | Admin |
| PUT | `/api/v1/admin/users/{id}/toggle` | Enable/disable user | Admin |
| GET | `/api/v1/health` | Health check | No |
| SignalR | `/hubs/chat` | Real-time chat streaming | Yes |

Full OpenAPI documentation is available at `/swagger` when running in development mode.

## Project Structure

```
KnowledgeHub.slnx
├── src/
│   ├── KnowledgeHub.Core/               # Domain entities, interfaces, enums (zero dependencies)
│   │   ├── Configuration/               # ChatSettings
│   │   ├── Entities/                     # BaseEntity, Document, DocumentChunk, Conversation,
│   │   │                                #   Message, MessageSource, VectorSearchResult
│   │   ├── Enums/                        # DocumentStatus, MessageRole
│   │   ├── Interfaces/
│   │   │   ├── Repositories/             # IRepository<T>, IDocumentRepository
│   │   │   └── Services/                 # IChatService, IFileStorageService,
│   │   │                                #   IDocumentTextExtractor, ITextChunker,
│   │   │                                #   IEmbeddingService, IVectorSearchService,
│   │   │                                #   IDocumentProcessingService, IDataSeeder
│   │   └── Models/                       # PaginationParams, PagedResult<T>
│   │
│   ├── KnowledgeHub.Infrastructure/      # EF Core, external service implementations
│   │   ├── Data/                         # ApplicationDbContext, ApplicationUser, DataSeeder,
│   │   │                                #   entity type configurations
│   │   ├── Repositories/                 # Repository<T>, DocumentRepository
│   │   └── Services/                     # Azure OpenAI, Azure AI Search, Blob Storage,
│   │                                    #   text extraction, chunking, document processing
│   │
│   ├── KnowledgeHub.API/                # ASP.NET Core Web API
│   │   ├── Configuration/               # JwtSettings, FileUploadSettings
│   │   ├── Controllers/                  # Auth, Documents, Chat, Admin, Health
│   │   ├── DTOs/                         # Request/response models
│   │   ├── Extensions/                   # ServiceCollectionExtensions (DI setup)
│   │   ├── Hubs/                         # ChatHub (SignalR streaming)
│   │   ├── Middleware/                   # SecurityHeaders, CorrelationId,
│   │   │                                #   RequestLogging, ExceptionHandling
│   │   └── Validators/                   # FluentValidation, FileSignatureValidator
│   │
│   └── KnowledgeHub.Web/                # Angular 21 frontend
│       └── src/app/
│           ├── core/                     # Auth service, interceptors, guards
│           ├── features/                 # auth, documents, chat modules
│           └── shared/                   # Reusable UI components
│
├── tests/
│   ├── KnowledgeHub.API.Tests/          # API unit + integration tests (xUnit)
│   └── KnowledgeHub.Infrastructure.Tests/ # Infrastructure tests (xUnit)
│
├── docs/                                 # Architecture documentation
├── docker-compose.yml                    # Container orchestration
└── docker-compose.override.yml           # Dev overrides
```

## Screenshots

_Screenshots will be added after UI finalization._

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
