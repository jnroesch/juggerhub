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
# Idempotent and safe to re-run: every step checks state before acting.
set -euo pipefail

# Only act in the remote (Claude Code on the web) environment.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

PROJECT_DIR="${CLAUDE_PROJECT_DIR:-$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)}"
CA_BUNDLE="/root/.ccr/ca-bundle.crt"
log() { echo "[session-start] $*" >&2; }

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
