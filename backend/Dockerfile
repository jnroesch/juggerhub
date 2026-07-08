# JuggerHub API — multi-stage build.
# Build context is ./backend (see docker-compose.yml); the .csproj is at its root.

# --- build: restore + publish the API ---------------------------------------
# SDK pinned to match backend/global.json and CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
WORKDIR /src

# Copy global.json first so `dotnet` enforces the pinned SDK for every command below.
COPY global.json ./

# Restore against just the project files first for better layer caching.
COPY JuggerHub.Api.csproj ./
COPY tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj tests/JuggerHub.Api.IntegrationTests/
RUN dotnet restore JuggerHub.Api.csproj

COPY . .
RUN dotnet publish JuggerHub.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# --- test: runs the xUnit + Testcontainers integration tests ----------------
# Used by docker-compose.test.yml (needs the Docker socket mounted for
# Testcontainers). Default command runs the suite and exits with its status.
FROM build AS test
WORKDIR /src
ENTRYPOINT ["dotnet", "test", \
  "tests/JuggerHub.Api.IntegrationTests/JuggerHub.Api.IntegrationTests.csproj", \
  "-c", "Release", "--logger", "console;verbosity=normal"]

# --- runtime: lean ASP.NET image with only the publish output ---------------
# ASP.NET runtime pinned (latest 10.0 patch at time of pinning).
FROM mcr.microsoft.com/dotnet/aspnet:10.0.7 AS runtime
WORKDIR /app

# curl is used by the compose healthcheck to probe /api/v1/health.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "JuggerHub.Api.dll"]
