# Multi-stage build for optimized image size
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy solution and project files first to maximize Docker layer caching.
COPY ["HngStageOne.sln", "./"]
COPY ["src/HngStageOne.Api/HngStageOne.Api.csproj", "src/HngStageOne.Api/"]
COPY ["tests/HngStageOne.Api.Tests/HngStageOne.Api.Tests.csproj", "tests/HngStageOne.Api.Tests/"]

# Restore dependencies
RUN dotnet restore "HngStageOne.sln"

# Copy the complete source tree
COPY . .

# Publish the application
RUN dotnet publish "src/HngStageOne.Api/HngStageOne.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage - use only ASP.NET Core runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Install curl for container health checks.
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

# Create data directory for SQLite persistence
RUN mkdir -p /app/data && chmod 777 /app/data

# Copy published application and bundled seed file
COPY --from=build /app/publish .
COPY seed_profiles.json ./seed_profiles.json

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Run the application
ENTRYPOINT ["dotnet", "HngStageOne.Api.dll"]
