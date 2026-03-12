# KnowledgeHub

AI-powered RAG (Retrieval-Augmented Generation) Chat Assistant with Knowledge Base management.

Upload documents, process them into searchable knowledge, and chat with an AI that provides answers grounded in your documents — with source citations.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| ORM | Entity Framework Core 10 |
| Frontend | Angular 21 |
| Database | SQLite (dev) / Azure SQL (prod) |
| Vector Store | Azure AI Search |
| AI | Azure OpenAI (GPT-4o + text-embedding-3-small) |
| File Storage | Local (dev) / Azure Blob Storage (prod) |
| Real-time | SignalR |
| Auth | ASP.NET Core Identity + JWT |
| CI/CD | GitHub Actions |
| Deploy | Azure App Service |

## Features

- **Document Management** — Upload PDF, DOCX, TXT, and Markdown files
- **RAG Pipeline** — Automatic text extraction, chunking, embedding, and vector indexing
- **AI Chat** — Ask questions and get answers grounded in your documents
- **Source Citations** — Every AI response includes references to source documents
- **Real-time Streaming** — Token-by-token response streaming via SignalR
- **Admin Dashboard** — User management and system analytics
- **Multi-language** — DE, EN, FR, IT
- **Dark/Light Theme** — User-selectable with system preference detection

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 22+](https://nodejs.org/)
- [Angular CLI 21](https://angular.dev/)

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

## Project Structure

```
KnowledgeHub.sln
├── src/
│   ├── KnowledgeHub.Core/          # Domain entities, interfaces, enums
│   ├── KnowledgeHub.Infrastructure/ # EF Core, services, AI integrations
│   ├── KnowledgeHub.API/           # Controllers, middleware, configuration
│   └── KnowledgeHub.Web/           # Angular 21 frontend
├── tests/
│   ├── KnowledgeHub.API.Tests/
│   └── KnowledgeHub.Infrastructure.Tests/
└── docs/                           # Architecture diagrams
```

## API Documentation

Once running, visit `/swagger` for the full OpenAPI documentation.

## License

MIT
