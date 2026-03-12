# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files for layer caching
COPY src/KnowledgeHub.Core/KnowledgeHub.Core.csproj src/KnowledgeHub.Core/
COPY src/KnowledgeHub.Infrastructure/KnowledgeHub.Infrastructure.csproj src/KnowledgeHub.Infrastructure/
COPY src/KnowledgeHub.API/KnowledgeHub.API.csproj src/KnowledgeHub.API/

RUN dotnet restore src/KnowledgeHub.API/KnowledgeHub.API.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/KnowledgeHub.API/KnowledgeHub.API.csproj -c Release -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
  CMD curl -f http://localhost:8080/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "KnowledgeHub.API.dll"]
