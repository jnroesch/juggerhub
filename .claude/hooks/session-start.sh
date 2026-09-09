#!/bin/bash
# SessionStart hook — prepares a Claude Code on the web session to build, run, and test
# the full Docker stack (README "Quick start"). It is a no-op outside the remote
# environment, so it never interferes with a local checkout.
#
# The remote environment routes ALL egress through a TLS-terminating policy proxy and
# blocks Docker Hub's layer CDN, which breaks a plain `docker compose up --build` three
# ways. This hook removes all three, matching the repo-side wiring (gcr mirror is a
# daemon setting; the proxy_ca build secret is defined in docker-compose*.yml):
#
#   1. The Docker daemon is not running     -> start dockerd.
#   2. Docker Hub's blob CDN is 403-blocked  -> pull docker.io images via mirror.gcr.io.
#   3. In-container builds distrust the proxy CA (npm/nuget fail on the MITM'd TLS)
#                                            -> export PROXY_CA_FILE so the optional
#                                               `proxy_ca` build secret carries the CA.
#
# It also seeds .env from .env.sample (compose refuses to start without JWT_SIGNING_KEY).
#
# Step 0 is the exception to "no-op outside the remote environment": the Postgres TLS material
# (feature 047) is generated in EVERY environment, because it is gitignored and the stack does
# not start without it.
#
# Idempotent and safe to re-run: every step checks state before acting.
set -euo pipefail

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
CA_BUNDLE="/root/.ccr/ca-bundle.crt"
log() { echo "[session-start] $*" >&2; }

# --- 0. Postgres TLS material (feature 047) ---------------------------------
# This step runs in EVERY environment, local included — deliberately ABOVE the remote-only
# guard below. Since feature 047 the backend connects with `SSL Mode=VerifyFull`, so the
# `database` service needs a certificate and the backend needs the CA that signed it. Without
# them `docker compose up` fails at the database container with `install: can't stat
# '/certs/server.key'`, which does not obviously mean "run a script".
#
# The files are gitignored, so a fresh clone or a new machine has none — this is what stops
# that being a manual step someone has to know about.
ensure_postgres_certs() {
  local dir="$PROJECT_DIR/certs/local"
  [ -f "$dir/server.key" ] && [ -f "$dir/server.crt" ] && [ -f "$dir/ca.crt" ] && return 0

  mkdir -p "$dir"

  # Preferred: the repo's own script, which is the single source of truth for what these
  # certificates contain (constitution VI — .ps1 only for repo scripts).
  if command -v pwsh >/dev/null 2>&1; then
    pwsh -NoProfile -File "$PROJECT_DIR/scripts/dev-postgres-certs.ps1" >&2 && {
      log "generated Postgres TLS material via dev-postgres-certs.ps1"
      return 0
    }
  fi

  # Fallback for environments without PowerShell (the remote Linux sandbox). Produces the
  # SAME shape as the .ps1 — and the SAN list below MUST be kept in step with it, because
  # `VerifyFull` matches the host string as written: `database` is the compose service name,
  # `localhost`/`127.0.0.1` cover `dotnet run` and `dotnet ef` from outside the stack.
  if command -v openssl >/dev/null 2>&1; then
    # A real temp file, not `<(...)`: process substitution hands openssl a /dev/fd path, which
    # some builds cannot open. A file costs one line and works everywhere.
    printf 'subjectAltName=DNS:database,DNS:localhost,IP:127.0.0.1\nbasicConstraints=critical,CA:FALSE\nkeyUsage=critical,digitalSignature,keyEncipherment\nextendedKeyUsage=serverAuth\n' > "$dir/server.ext"

    openssl req -x509 -newkey rsa:4096 -sha256 -days 1825 -nodes \
      -keyout "$dir/ca.key" -out "$dir/ca.crt" \
      -subj "/CN=JuggerHub Local Development CA/O=JuggerHub" \
      -addext "basicConstraints=critical,CA:TRUE" \
      -addext "keyUsage=critical,keyCertSign,cRLSign" >/dev/null 2>&1
    openssl req -new -newkey rsa:2048 -nodes \
      -keyout "$dir/server.key" -out "$dir/server.csr" \
      -subj "/CN=database/O=JuggerHub" >/dev/null 2>&1
    openssl x509 -req -in "$dir/server.csr" -CA "$dir/ca.crt" -CAkey "$dir/ca.key" \
      -CAcreateserial -out "$dir/server.crt" -days 1825 -sha256 \
      -extfile "$dir/server.ext" >/dev/null 2>&1
    # The CA key is discarded once it has signed, matching what the .ps1 leaves behind: nothing
    # re-signs against this CA, and an unused private key sitting on disk is worse than absent.
    rm -f "$dir/server.csr" "$dir/server.ext" "$dir/ca.srl" "$dir/ca.key"

    # Verify rather than assume: a silently half-written cert directory would surface later as
    # an unexplained database container failure.
    if [ ! -f "$dir/server.crt" ] || [ ! -f "$dir/ca.crt" ]; then
      log "WARNING: openssl did not produce a certificate — run ./scripts/dev-postgres-certs.ps1"
      return 0
    fi

    log "generated Postgres TLS material via openssl (no pwsh available)"
    return 0
  fi

  log "WARNING: no pwsh or openssl — run ./scripts/dev-postgres-certs.ps1 before 'docker compose up'"
  return 0
}

ensure_postgres_certs

# Everything below only applies to the remote (Claude Code on the web) environment.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# 1. Seed .env from the sample (local dev defaults work out of the box).
if [ ! -f "$PROJECT_DIR/.env" ] && [ -f "$PROJECT_DIR/.env.sample" ]; then
  cp "$PROJECT_DIR/.env.sample" "$PROJECT_DIR/.env"
  log "seeded .env from .env.sample"
fi

# 2. Expose the egress-proxy CA to the image builds via the compose `proxy_ca` secret.
#    Persisted for the whole session so every docker/compose command inherits it.
if [ -f "$CA_BUNDLE" ] && [ -n "${CLAUDE_ENV_FILE:-}" ]; then
  echo "export PROXY_CA_FILE=$CA_BUNDLE" >> "$CLAUDE_ENV_FILE"
  log "PROXY_CA_FILE -> $CA_BUNDLE"
fi
export PROXY_CA_FILE="$CA_BUNDLE"

# 3. Configure the Docker daemon to pull docker.io images through mirror.gcr.io, whose
#    host is reachable while Docker Hub's own blob CDN (production.cloudfront.docker.com)
#    is blocked by the egress policy. Only official + public Hub images are needed here.
if [ ! -f /etc/docker/daemon.json ] || ! grep -q 'mirror.gcr.io' /etc/docker/daemon.json 2>/dev/null; then
  sudo mkdir -p /etc/docker
  echo '{ "registry-mirrors": ["https://mirror.gcr.io"] }' | sudo tee /etc/docker/daemon.json >/dev/null
  log "wrote /etc/docker/daemon.json (registry mirror)"
fi

# 4. Start the Docker daemon if it is not already running.
if ! docker info >/dev/null 2>&1; then
  sudo dockerd >/tmp/dockerd.log 2>&1 &
  for _ in $(seq 1 20); do
    docker info >/dev/null 2>&1 && break
    sleep 1
  done
  if docker info >/dev/null 2>&1; then
    log "dockerd started"
  else
    log "WARNING: dockerd did not become ready — see /tmp/dockerd.log"
  fi
else
  log "dockerd already running"
fi

log "ready: run 'docker compose up -d --build' to bring up the stack"
exit 0
