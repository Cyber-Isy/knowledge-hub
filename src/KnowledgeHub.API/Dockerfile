# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY KnowledgeHub.slnx ./
COPY src/KnowledgeHub.Core/KnowledgeHub.Core.csproj src/KnowledgeHub.Core/
COPY src/KnowledgeHub.Infrastructure/KnowledgeHub.Infrastructure.csproj src/KnowledgeHub.Infrastructure/
COPY src/KnowledgeHub.API/KnowledgeHub.API.csproj src/KnowledgeHub.API/
COPY tests/KnowledgeHub.API.Tests/KnowledgeHub.API.Tests.csproj tests/KnowledgeHub.API.Tests/
COPY tests/KnowledgeHub.Infrastructure.Tests/KnowledgeHub.Infrastructure.Tests.csproj tests/KnowledgeHub.Infrastructure.Tests/

RUN dotnet restore KnowledgeHub.slnx

# Copy everything and publish
COPY . .
RUN dotnet publish src/KnowledgeHub.API/KnowledgeHub.API.csproj -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1

ENTRYPOINT ["dotnet", "KnowledgeHub.API.dll"]
