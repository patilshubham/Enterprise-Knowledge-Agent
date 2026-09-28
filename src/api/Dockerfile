# syntax=docker/dockerfile:1.7
# Build context: repository root (needed for cross-project references).
#   docker build -f src/api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level version/build config and just the .csproj files first so
# `dotnet restore` is cached independently of source-code changes.
COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/api/EnterpriseKnowledgeAgent.Api.csproj src/api/
COPY src/application/EnterpriseKnowledgeAgent.Application.csproj src/application/
COPY src/domain/EnterpriseKnowledgeAgent.Domain.csproj src/domain/
COPY src/infrastructure/EnterpriseKnowledgeAgent.Infrastructure.csproj src/infrastructure/
COPY src/shared/EnterpriseKnowledgeAgent.Shared.csproj src/shared/
RUN dotnet restore src/api/EnterpriseKnowledgeAgent.Api.csproj

# Now copy the actual source and publish.
COPY src/api/ src/api/
COPY src/application/ src/application/
COPY src/domain/ src/domain/
COPY src/infrastructure/ src/infrastructure/
COPY src/shared/ src/shared/
RUN dotnet publish src/api/EnterpriseKnowledgeAgent.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

# curl is used by the compose healthcheck to poll /health/ready.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# The dataprotection-keys named volume (see docker-compose.yml) is created by
# Docker with root ownership on first use. Pre-create + chown the mount point
# here so it's owned by the unprivileged "app" user before the volume's
# (empty) content is copied in on first attach - otherwise KeyRingProvider
# fails with UnauthorizedAccessException when it tries to write key XML files.
RUN mkdir -p /data/dataprotection-keys && chown -R app:app /data/dataprotection-keys

# Run as the built-in unprivileged "app" user shipped in Microsoft's .NET
# runtime images (since .NET 8) instead of root.
USER app

ENTRYPOINT ["dotnet", "EnterpriseKnowledgeAgent.Api.dll"]
