# KnowledgeHub — Project Instructions

## Overview
AI-powered RAG Chat Assistant with Knowledge Base management. Built with .NET 10 (ASP.NET Core Web API), Angular 21, Entity Framework Core, Azure OpenAI, and Azure AI Search.

## Tech Stack
- **Backend**: ASP.NET Core Web API (.NET 10), EF Core 10, ASP.NET Core Identity + JWT
- **Frontend**: Angular 21 (standalone components, signals), Tailwind CSS
- **Database**: SQLite (dev) / Azure SQL (prod)
- **AI**: Azure OpenAI (GPT-4o + text-embedding-3-small), Azure AI Search
- **Real-time**: SignalR
- **File Storage**: Local disk (dev) / Azure Blob Storage (prod)

## Project Structure
```
KnowledgeHub.sln
├── src/
│   ├── KnowledgeHub.Core/           # Domain entities, interfaces, enums (zero dependencies)
│   ├── KnowledgeHub.Infrastructure/ # EF Core, external service implementations
│   ├── KnowledgeHub.API/            # ASP.NET Core Web API (controllers, middleware, config)
│   └── KnowledgeHub.Web/            # Angular 21 frontend
├── tests/
│   ├── KnowledgeHub.API.Tests/      # API unit + integration tests (xUnit)
│   └── KnowledgeHub.Infrastructure.Tests/ # Infrastructure tests (xUnit)
```

## Development Commands

### Backend (.NET)
```bash
cd src/KnowledgeHub.API
dotnet restore                       # Restore packages
dotnet build                         # Build
dotnet run                           # Run API (https://localhost:5001)
dotnet watch run                     # Run with hot reload
dotnet test ../../tests/KnowledgeHub.API.Tests/
dotnet test ../../tests/KnowledgeHub.Infrastructure.Tests/
```

### EF Core Migrations
```bash
cd src/KnowledgeHub.API
dotnet ef migrations add <Name> --project ../KnowledgeHub.Infrastructure
dotnet ef database update --project ../KnowledgeHub.Infrastructure
```

### Frontend (Angular)
```bash
cd src/KnowledgeHub.Web
npm install                          # Install dependencies
ng serve                             # Dev server (http://localhost:4200)
ng build                             # Production build
ng test                              # Run Vitest tests
ng lint                              # Run ESLint
```

### Full Stack (Docker)
```bash
docker compose up                    # Start all services
docker compose down                  # Stop all services
```

## Architecture

### Clean Architecture Layers
- **Core**: Domain entities, interfaces, enums, DTOs — no external dependencies
- **Infrastructure**: EF Core DbContext, repository implementations, Azure service integrations
- **API**: Controllers, middleware, DI configuration — references Infrastructure and Core

### RAG Pipeline
1. Upload document → validate → store file
2. Extract text (PDF/DOCX/TXT) → chunk with overlap → generate embeddings
3. Index chunks in vector store
4. Chat query → embed query → vector search → build prompt with context → generate response
5. Return response with source citations

### Key Patterns
- Repository pattern with generic `IRepository<T>`
- DI-based service swapping (local ↔ Azure implementations)
- Background document processing via `IHostedService`
- SignalR for real-time chat streaming
- JWT Bearer authentication on all protected endpoints

## Conventions
- All code in English
- Follow C# coding conventions (PascalCase for public, camelCase for private)
- Angular: standalone components, signals for state, reactive forms + validation
- Commit messages: imperative mood, no AI/assistant mentions
- Environment secrets in `.env` / `appsettings.Development.json` (never committed)
