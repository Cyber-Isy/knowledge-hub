# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview
AI-powered RAG Chat Assistant with Knowledge Base management. Built with .NET 10 (ASP.NET Core Web API), Angular 21, Entity Framework Core, Azure OpenAI, and Azure AI Search.

## Development Commands

### Backend (.NET)
```bash
dotnet build KnowledgeHub.slnx                                        # Build entire solution
dotnet run --project src/KnowledgeHub.API                              # Run API (https://localhost:5001)
dotnet watch run --project src/KnowledgeHub.API                        # Run with hot reload
dotnet test KnowledgeHub.slnx                                         # Run all tests
dotnet test tests/KnowledgeHub.API.Tests                               # Run API tests only
dotnet test tests/KnowledgeHub.Infrastructure.Tests                    # Run infrastructure tests only
dotnet test --filter "FullyQualifiedName~AuthController"               # Run single test class
```

### EF Core Migrations (run from repo root)
```bash
dotnet ef migrations add <Name> --project src/KnowledgeHub.Infrastructure --startup-project src/KnowledgeHub.API
dotnet ef database update --project src/KnowledgeHub.Infrastructure --startup-project src/KnowledgeHub.API
```

### Frontend (Angular)
```bash
cd src/KnowledgeHub.Web
npm install                   # Install dependencies
npx ng serve                  # Dev server (http://localhost:4200), proxies /api to localhost:5001
npx ng build                  # Production build
npx ng test                   # Run Vitest tests
npx ng lint                   # Run ESLint
```

### Docker
```bash
docker compose up             # Start API (port 5001) + Angular (port 4200)
docker compose down           # Stop all services
```

## Architecture

### Clean Architecture (dependency flow: Core ← Infrastructure ← API)
- **KnowledgeHub.Core** — Domain entities, interfaces, enums. Zero external dependencies. All service contracts live here (`Interfaces/Services/`, `Interfaces/Repositories/`).
- **KnowledgeHub.Infrastructure** — EF Core `ApplicationDbContext`, repository implementations, all external service integrations (Azure OpenAI, AI Search, Blob Storage, text extractors). `ApplicationUser` (Identity) lives here, not in Core.
- **KnowledgeHub.API** — Controllers, middleware pipeline, DI wiring, DTOs, FluentValidation validators. `ServiceCollectionExtensions.cs` is the composition root.
- **KnowledgeHub.Web** — Angular 21 SPA. Standalone components, signals for state, `@if`/`@for` control flow, functional guards/interceptors, lazy-loaded routes.

### DI-Based Service Swapping
The API swaps implementations based on environment in `ServiceCollectionExtensions.AddApplicationServices()`:
- **File storage**: `LocalFileStorageService` (dev, writes to `uploads/`) ↔ `AzureBlobStorageService` (prod)
- **Database**: SQLite (dev) ↔ Azure SQL with retry policy (prod), controlled by `ASPNETCORE_ENVIRONMENT`

### RAG Pipeline
Document upload triggers background processing via `System.Threading.Channels`:
1. `DocumentsController` → enqueues document ID to channel
2. `DocumentProcessingBackgroundService` (IHostedService) → dequeues and calls `DocumentProcessingService`
3. Pipeline: extract text (`PdfTextExtractor`/`DocxTextExtractor`/`PlainTextExtractor` via factory) → chunk (`RecursiveTextChunker`, 512 tokens, 50 overlap) → embed (`AzureOpenAIEmbeddingService`) → index (`AzureAISearchService`)
4. `DocumentStatus` transitions: Uploaded → Processing → Chunking → Embedding → Indexing → Ready (or Failed)

### Middleware Pipeline Order (Program.cs)
SecurityHeaders → CorrelationId → RequestLogging → ExceptionHandling → Swagger (dev) → HTTPS → CORS → RateLimiter → Auth → Controllers + SignalR

### Angular Frontend
- i18n via `@ngx-translate` with JSON files in `src/KnowledgeHub.Web/src/assets/i18n/` (de, en, fr, it)
- Auth state managed via signals in `AuthService` — `currentUser`, `isAuthenticated` (computed)
- All HTTP requests get JWT via `authInterceptor`; 401 responses trigger logout
- Routes: `/login`, `/register` (public); `/dashboard`, `/documents`, `/chat`, `/chat/:conversationId`, `/admin/*` (protected)
- SignalR connection for real-time chat streaming at `/hubs/chat` (JWT via query string)

### API Versioning
URL-based: all endpoints at `/api/v1/...`. Configured via `Asp.Versioning` with `UrlSegmentApiVersionReader`.

### Rate Limiting
Three policies: `auth` (10/min per IP), `upload` (20/hr per user), `chat` (60/hr per user). Applied per-controller via `[EnableRateLimiting]`.

### Testing
- **Unit tests**: xUnit + Moq, test controllers with mocked services
- **Integration tests**: `WebApplicationFactory<Program>` with `CustomWebApplicationFactory` (InMemory database), shared via `IntegrationTestCollection`

## Conventions
- All code in English
- C#: PascalCase public, camelCase private, file-scoped namespaces
- Angular: standalone components, signals (not BehaviorSubject), `inject()` function (not constructor DI)
- Commit messages: imperative mood, no AI/assistant mentions
- Solution file is `.slnx` (new XML format), not `.sln`
- Dev database auto-migrates and seeds on startup (demo@knowledgehub.ch / Demo1234!, admin@knowledgehub.ch / Admin1234!)
