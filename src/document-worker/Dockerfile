# syntax=docker/dockerfile:1.7
# Build context: repository root (needed for cross-project references).
#   docker build -f src/document-worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/document-worker/EnterpriseKnowledgeAgent.DocumentWorker.csproj src/document-worker/
COPY src/application/EnterpriseKnowledgeAgent.Application.csproj src/application/
COPY src/domain/EnterpriseKnowledgeAgent.Domain.csproj src/domain/
COPY src/infrastructure/EnterpriseKnowledgeAgent.Infrastructure.csproj src/infrastructure/
COPY src/shared/EnterpriseKnowledgeAgent.Shared.csproj src/shared/
RUN dotnet restore src/document-worker/EnterpriseKnowledgeAgent.DocumentWorker.csproj

COPY src/document-worker/ src/document-worker/
COPY src/application/ src/application/
COPY src/domain/ src/domain/
COPY src/infrastructure/ src/infrastructure/
COPY src/shared/ src/shared/
RUN dotnet publish src/document-worker/EnterpriseKnowledgeAgent.DocumentWorker.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

# Uses the ASP.NET Core runtime image (not the slim base runtime) because the
# worker app depends on the Microsoft.AspNetCore.App shared framework via
# Serilog.AspNetCore and OpenTelemetry hosting - the base dotnet/runtime image
# ships only Microsoft.NETCore.App and the app fails at launch with
# "No frameworks were found" (Microsoft.AspNetCore.App).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV DOTNET_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Run as the built-in unprivileged "app" user shipped in Microsoft's .NET
# runtime images (since .NET 8) instead of root.
USER app

ENTRYPOINT ["dotnet", "EnterpriseKnowledgeAgent.DocumentWorker.dll"]
