# JuggerHub API — multi-stage build.
# Build context is ./backend (see docker-compose.yml); the .csproj is at its root.

# --- build: restore + publish the API ---------------------------------------
# SDK pinned to match backend/global.json and CI.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

# Optional egress-proxy CA. Behind a TLS-terminating egress proxy (e.g. Claude Code on
# the web) `dotnet restore` fails against NuGet with "UntrustedRoot" unless the proxy's
# CA is trusted. The CA arrives as an OPTIONAL build secret (`proxy_ca`, wired in the
# docker-compose*.yml files from ${PROXY_CA_FILE:-/dev/null}). With no secret the mount
# is empty and this step is a no-op, so ordinary local builds and CI are unaffected.
RUN --mount=type=secret,id=proxy_ca,required=false \
    if [ -s /run/secrets/proxy_ca ]; then \
      cp /run/secrets/proxy_ca /usr/local/share/ca-certificates/proxy-ca.crt && update-ca-certificates; \
    fi

# Copy global.json first so `dotnet` enforces the pinned SDK for every command below.
COPY global.json ./

# Shared build settings (TargetFramework, analyzers) live in Directory.Build.props
# and .editorconfig (feature 027). They MUST be present before restore/build,
# otherwise MSBuild sees an empty TargetFramework and restore fails (NETSDK1013).
COPY Directory.Build.props .editorconfig ./

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS runtime
WORKDIR /app

# curl is used by the compose healthcheck to probe /api/v1/health.
# Acquire::Retries hardens the step against transient Ubuntu mirror failures
# (e.g. hash-sum mismatches) that otherwise flake the build-backend CI job (#87).
RUN apt-get update -o Acquire::Retries=3 \
    && apt-get install -y --no-install-recommends -o Acquire::Retries=3 curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "JuggerHub.Api.dll"]
